﻿namespace RepoIntegrityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.XPath;
    using NuGet.Protocol;
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
                        var disableAutoVersionRange = string.Equals(pkgRef.Attribute("AutomaticVersionRange")?.Value, "false", StringComparison.OrdinalIgnoreCase);

                        if (versionStr is null)
                        {
                            // This will have to change if we start supporting central package management in all repos as there could be a VersionOverride attribute
                            continue;
                        }

                        var isSingleVersion = NuGetVersion.TryParse(versionStr, out var version);
                        if (!isSingleVersion)
                        {
                            f.Fail($"Dependency '{name}' should use a single version number because tooling is now used to generate version ranges at compile time.");
                            continue;
                        }

                        if (PackageShouldNotGenerateVersionRange(name, out var trustedPrefix) && !disableAutoVersionRange)
                        {
                            f.Fail($"Dependency '{name}' should include AutomaticVersionRange=\"false\" because the prefix '{trustedPrefix}' is trusted to not introduce breaking changes.");
                        }

                        // Eventually, when these tests are run in release workflows, remove `|| true` to only break when attempting a release
                        if (isRtmRelease || true)
                        {
                            if (version.IsPrerelease)
                            {
                                f.Fail($"Dependency '{name}' cannot use a prerelease package on an RTM release.");
                            }
                        }
                    }
                });
        }

        [Test]
        public void ComponentDependenciesShouldNotBeDuplicatedInTests()
        {
            List<(string projectFileName, string rangedDependency)> publicDeps = [];

            new TestRunner("*.csproj", "Find public dependencies")
                .ProjectsProducingNuGetPackages()
                .Run(f =>
                {
                    var packageReferenceElements = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageReferenceElements)
                    {
                        var name = pkgRef.Attribute("Include").Value;
                        var versionStr = pkgRef.Attribute("Version")?.Value;
                        var isSingleVersion = NuGetVersion.TryParse(versionStr, out var version);
                        var privateAssets = pkgRef.Attribute("PrivateAssets")?.Value is not null;

                        if (isSingleVersion && !privateAssets)
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

                        var publicDeps = lookup[projectFileName] ?? [];

                        foreach (var depName in publicDeps)
                        {
                            var findPackageRef = f.XDocument.XPathSelectElement($"/Project/ItemGroup/PackageReference[@Include='{depName}']");
                            if (findPackageRef is not null)
                            {
                                f.Fail($"Test project '{f.FileName}' has a project reference to '{projectFileName}' and should not repeat the PackageReference for '{depName}'.");
                            }
                        }
                    }
                });
        }

        [Test]
        public void KnownPackagesArePrivateAssetsAll()
        {
            new TestRunner("*.csproj", "Package references for known build tools should be marked with PrivateAssets=\"All\"")
                .Run(f =>
                {
                    var packageRefs = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");
                    var sourcePackage = f.XDocument.XPathSelectElement("/Project/PropertyGroup/IncludeSourceFilesInPackage").GetBoolean() ?? false;

                    foreach (var pkgRef in packageRefs)
                    {
                        if (pkgRef.Attribute("PrivateAssets")?.Value == "All")
                        {
                            continue;
                        }

                        var packageName = pkgRef.Attribute("Include").Value;

                        if (KnownBuildToolPackages.Contains(packageName))
                        {
                            f.Fail($"PackageReference '{packageName}' must be marked PrivateAssets=\"All\"");
                        }
                        else if (sourcePackage && AlsoPrivateAssetsInSourcePackages.Contains(packageName))
                        {
                            f.Fail($"Because this is a source package, PackageReference '{packageName}' also must be marked PrivateAssets=\"All\"");
                        }
                    }
                });
        }

        static readonly HashSet<string> KnownBuildToolPackages = new([
            "Particular.Packaging",
            "Particular.CodeRules",
            "Particular.Analyzers",
            "Fody",
            "Obsolete.Fody",
            "Janitor.Fody",
            "ILRepack",
            "NServiceBus.Transport.Msmq.Sources"
        ], StringComparer.OrdinalIgnoreCase);

        static readonly HashSet<string> AlsoPrivateAssetsInSourcePackages = new([
            "GitHubActionsTestLogger",
            "Microsoft.NET.Test.Sdk",
            "NUnit3TestAdapter",
            "Particular.Approvals"
        ], StringComparer.OrdinalIgnoreCase);

        [Test]
        public void DontUseMockingFrameworks()
        {
            new TestRunner("*.csproj", "Projects should not reference mocking frameworks")
                .Run(f =>
                {
                    var packageRefs = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageRefs)
                    {
                        var packageName = pkgRef.Attribute("Include").Value;

                        if (KnownMockingFrameworks.Contains(packageName))
                        {
                            f.Fail($"Replace usage of {packageName} with a simple hand-rolled fake");
                        }
                    }
                });
        }
        static readonly HashSet<string> KnownMockingFrameworks = new([
            "FakeItEasy",
            "Moq",
            "NSubstitute"
        ], StringComparer.OrdinalIgnoreCase);

        [Test]
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

        [Test]
        public async Task ShouldNotReferenceDeprecatedPackages()
        {
            await new TestRunner("*.csproj", "Projects should not reference deprecated NuGet packages")
                .SdkProjects()
                .RunAsync(async f =>
                {
                    var packageNames = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference")
                        .Select(p => p.Attribute("Include")?.Value)
                        .Where(name => name != null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    foreach (var packageName in packageNames)
                    {
                        var package = await NuGetData.GetPackageInfo(packageName);
                        if (package is null)
                        {
                            continue; // Internal packages not on NuGet
                        }

                        // Implementation returns this synchronously, no need for additional caching
                        var deprecationData = await package.GetDeprecationMetadataAsync();
                        if (deprecationData is not null)
                        {
                            var reasons = string.Join(", ", deprecationData.Reasons);
                            f.Fail($"Package '{packageName}' has been deprecated for reasons ({reasons}) and is no longer maintained.");
                        }
                    }
                });
        }



        static bool PackageShouldNotGenerateVersionRange(string name, out string trustedPrefix)
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
