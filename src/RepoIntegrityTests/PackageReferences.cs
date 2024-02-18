namespace RepoIntegrityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.XPath;
    using NuGet.Versioning;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    [DotNetProjects]
    public partial class PackageReferences
    {
        [Test]
        public void PrivateAssetsAsAttributesNotElements()
        {
            new TestRunner("*.csproj", "Package references should have PrivateAssets/IncludeAssets as attributes, not child elements")
                .SdkProjects()
                .Run(file =>
                {
                    var privateAssetElements = file.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference/PrivateAssets");
                    var includeAssetElements = file.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference/IncludeAssets");

                    if (privateAssetElements.Any() || includeAssetElements.Any())
                    {
                        file.Fail();
                    }
                });

        }

        [Test]
        public void DoNotMixReferenceTypes()
        {
            new TestRunner("*.csproj", "PackageReference, ProjectReference, and other item types should not be mixed within the same ItemGroup")
                .SdkProjects()
                .Run(file =>
                {
                    var itemGroups = file.XDocument.XPathSelectElements("/Project/ItemGroup");

                    foreach (var itemGroup in itemGroups)
                    {
                        if (itemGroup.HasElements)
                        {
                            var childNames = itemGroup.Elements().Select(e => e.Name.LocalName).Distinct().ToArray();

                            if (childNames.Length > 1 && childNames.Any(name => ReferenceElementNames.Contains(name)))
                            {
                                file.Fail("ItemGroup mixes " + string.Join(", ", childNames));
                                return;
                            }
                        }
                    }
                });
        }

        [Test]
        public void AbsoluteVersionsInTestProjects()
        {
            new TestRunner("*.csproj", "Test projects should use absolute versions of dependencies so that Dependabot can update them")
                .TestProjects()
                .Run(f =>
                {
                    // Except for things like Core test source packages like TransportTests, they need to control dependency ranges
                    if (f.ProducesNuGetPackage())
                    {
                        return;
                    }

                    var packageReferenceElements = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageReferenceElements)
                    {
                        var name = pkgRef.Attribute("Include").Value;
                        var versionStr = pkgRef.Attribute("Version")?.Value;
                        if (versionStr is not null)
                        {
                            if (!NuGetVersion.TryParse(versionStr, out var version))
                            {
                                f.Fail($"PackageReference '{name}' is using version '{versionStr}' which should be an explicit version");
                            }
                        }
                    }
                });
        }

        [Test]
        public void NoPrereleasePackagesOnRelease()
        {
            var githubRef = Environment.GetEnvironmentVariable("GITHUB_REF");

            var isRtmRelease = !string.IsNullOrEmpty(githubRef) && NonPrereleaseGithubRefRegex().IsMatch(githubRef);

            new TestRunner("*.csproj", "Non-prerelease packages cannot have prerelease dependencies")
                .ProjectsProducingNuGetPackages()
                .Run(f =>
                {
                    var packageReferenceElements = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageReferenceElements)
                    {
                        var name = pkgRef.Attribute("Include").Value;
                        var versionStr = pkgRef.Attribute("Version")?.Value;
                        var privateAssets = pkgRef.Attribute("PrivateAssets")?.Value is not null;

                        if (versionStr is null)
                        {
                            // This will have to change if we start supporting central package management in all repos as there could be a VersionOverride attribute
                            continue;
                        }

                        var isSingleVersion = NuGetVersion.TryParse(versionStr, out var version);
                        var isRange = VersionRange.TryParse(versionStr, out var range);

                        if (privateAssets)
                        {
                            if (!isSingleVersion)
                            {
                                f.Fail($"Dependency '{name}' with PrivateAssets should use a single version so that Dependabot can update it.");
                            }
                        }
                        else if (PackageDoesNotRequireVersionRanges(name, out var trustedPrefix))
                        {
                            if (!isSingleVersion)
                            {
                                f.Fail($"Dependency '{name}' should use a single version because the prefix '{trustedPrefix}' is trusted to not introduce breaking changes.");
                            }
                        }
                        else
                        {
                            if (!isRange || !range.HasLowerAndUpperBounds)
                            {
                                bool isSingleVersionPrerelease = isSingleVersion && version.IsPrerelease;
                                if (!isSingleVersionPrerelease)
                                {
                                    f.Fail($"Dependency '{name}' should be defined as a version range so that users can't accidentally update to a version with breaking changes.");
                                }
                            }
                        }

                        // Only on a release workflow for an RTM release
                        if (isRtmRelease)
                        {
                            var minVersionPrerelease = range.MinVersion?.IsPrerelease ?? false;
                            var maxVersionPrerelease = range.MaxVersion?.IsPrerelease ?? false;
                            if (minVersionPrerelease || maxVersionPrerelease)
                            {
                                f.Fail($"Dependency '{name}' cannot use a prerelease package on an RTM release.");
                            }
                        }
                    }
                });
        }

        [Test]
        public void DependenciesDefinedAsRangesMustBeSpecifiedInTests()
        {
            List<(string projectFileName, string rangedDependency)> publicDeps = [];

            new TestRunner("*.csproj", "Find public dependencies that are version-ranged")
                .ProjectsProducingNuGetPackages()
                .Run(f =>
                {
                    var packageReferenceElements = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageReferenceElements)
                    {
                        var name = pkgRef.Attribute("Include").Value;
                        var versionStr = pkgRef.Attribute("Version")?.Value;

                        if (VersionRange.TryParse(versionStr, out var range) && range.HasLowerAndUpperBounds)
                        {
                            var projectFileName = Path.GetFileName(f.FullPath);
                            publicDeps.Add((projectFileName, name));
                        }
                    }
                });

            var lookup = publicDeps.ToLookup(dep => dep.projectFileName, dep => dep.rangedDependency);

            new TestRunner("*.csproj", "Ensure component dependency ranges have absolute version in test projects")
                .TestProjects()
                .Run(f =>
                {
                    if (f.ProducesNuGetPackage())
                    {
                        return;
                    }

                    var projectRefs = f.XDocument.XPathSelectElements("/Project/ItemGroup/ProjectReference");

                    foreach (var projRef in projectRefs)
                    {
                        var relativePath = projRef.Attribute("Include").Value;
                        var parts = relativePath.Split('\\');
                        var projectFileName = parts.Last();

                        var rangedDeps = lookup[projectFileName] ?? [];

                        foreach (var depName in rangedDeps)
                        {
                            var findPackageRef = f.XDocument.XPathSelectElement($"/Project/ItemGroup/PackageReference[@Include='{depName}']");
                            if (findPackageRef is null || !NuGetVersion.TryParse(findPackageRef.Attribute("Version")?.Value, out _))
                            {
                                f.Fail($"Test project '{f.FileName}' has a project reference to '{projectFileName}' but does not specify an explicit version for the ranged dependency '{depName}'.");
                            }
                        }
                    }
                });
        }

        public void DontExplicitlyReferenceParticularAnalyzers()
        {
            new TestRunner("*.csproj", "Projects should not explicitly reference Particular.Analyzers since it's referenced by Directory.Build.props")
                .SdkProjects()
                .Run(f =>
                {
                    var analyzers = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference[@Include='Particular.Analyzers']");
                    var coderules = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference[@Include='Particular.Analyzers']");

                    if (analyzers.Any() || coderules.Any())
                    {
                        f.Fail();
                    }
                });
        }

        static bool PackageDoesNotRequireVersionRanges(string name, out string trustedPrefix)
        {
            foreach (var prefix in packagePrefixesNotRequiringVersionRanges)
            {
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    trustedPrefix = prefix;
                    return true;
                }
            }

            trustedPrefix = null;
            return false;
        }

        static readonly string[] packagePrefixesNotRequiringVersionRanges = [
            "System.",
            "Microsoft.Extensions."
        ];

        // Other possibilities: Content, None, EmbeddedResource, Compile, InternalsVisibleTo, Artifact, RemoveSourceFileFromPackage, Folder
        static readonly HashSet<string> ReferenceElementNames = ["ProjectReference", "PackageReference", "Reference", "FrameworkReference"];

        [GeneratedRegex(@"^refs/tags/\d+.\d+.\d+$", RegexOptions.Compiled)]
        private static partial Regex NonPrereleaseGithubRefRegex();
    }
}
