using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using NuGet.Versioning;
using NUnit.Framework;

namespace RepoIntegrityTests
{
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
                        var versionStr = pkgRef.Attribute("Version")?.Value;
                        if (versionStr is not null)
                        {
                            if (!NuGetVersion.TryParse(versionStr, out var version))
                            {
                                f.Fail();
                            }
                        }
                    }
                });
        }

        [Test]
        public void NoPrereleasePackagesOnRelease()
        {
            var githubRef = Environment.GetEnvironmentVariable("GITHUB_REF") ?? "refs/tags/1.1.10";

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
                                f.Fail($"Dependency '{name}' should be defined as a version range so that users can't accidentally update to a version with breaking changes.");
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
