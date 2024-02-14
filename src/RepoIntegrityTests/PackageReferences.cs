using System.Collections.Generic;
using System.Linq;
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
        public void VersionRangesInPackableProjects()
        {
            new TestRunner("*.csproj", "In packable projects, NServiceBus dependencies should be a version range except when set to a prerelease version")
                .ProjectsProducingNuGetPackages()
                .Run(f =>
                {
                    var packageReferenceElements = f.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference");

                    foreach (var pkgRef in packageReferenceElements)
                    {
                        var package = pkgRef.Attribute("Include").Value;
                        var versionStr = pkgRef.Attribute("Version")?.Value;
                        if (versionStr is not null && package.StartsWith("NServiceBus"))
                        {
                            if (NuGetVersion.TryParse(versionStr, out var version))
                            {
                                if (!version.IsPrerelease)
                                {
                                    f.Fail($"Package '{package}' version '{versionStr}' is not a prerelease and should be defined as a range [A.B.C, X.0.0) where usually X = A + 1");
                                }
                            }
                            else if (VersionRange.TryParse(versionStr, out var range))
                            {
                                if (!range.IsMinInclusive || range.IsMaxInclusive)
                                {
                                    f.Fail($"Package '{package}' version '{versionStr}' should be of the form [A.B.C, X.0.0) where usually X = A + 1");
                                }
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

        // Other possibilities: Content, None, EmbeddedResource, Compile, InternalsVisibleTo, Artifact, RemoveSourceFileFromPackage, Folder
        static readonly HashSet<string> ReferenceElementNames = ["ProjectReference", "PackageReference", "Reference", "FrameworkReference"];
    }
}
