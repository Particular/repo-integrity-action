namespace RepoIntegrityTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.XPath;
    using Microsoft.VisualBasic;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    [DotNetProjects, FixtureLifeCycle(LifeCycle.SingleInstance)]
    public partial class InternalsVisibleToTests
    {
        Dictionary<string, FileContext> projects = [];

        [OneTimeSetUp]
        public void FindProjects()
        {
            new TestRunner("*.csproj", "Find package and test project files")
                .Run(f => projects.Add(f.FullPath, f));
        }

        [Test]
        public void ShouldBeInProjectFile()
        {
            string[] filenames =
            [
                "InternalsVisibleTo.cs",
                "AssemblyInfo.cs"
            ];

            foreach (var filename in filenames)
            {
                new TestRunner(filename, "InternalsVisibleTo should be registered in project files", failIfNoMatches: false)
                    .Run(CheckFileForInternalsVisibleToAttribute);
            }
        }

        void CheckFileForInternalsVisibleToAttribute(FileContext f)
        {
            var project = FindProjectForCodeFile(f.FullPath);
            var projectIsSigned = project.XDocument.XPathSelectElement("/Project/PropertyGroup/SignAssembly").GetBoolean() ?? false;

            var matches = InternalsVisibleToRegex().Matches(File.ReadAllText(f.FullPath));

            foreach (Match match in matches)
            {
                var visibleTo = match.Groups[1].Value;

                if (visibleTo.Contains(','))
                {
                    visibleTo = visibleTo.Split(',')[0];
                }

                var visibleToProject = projects.Values.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p.FullPath) == visibleTo);

                if (visibleToProject is null)
                {
                    f.Fail($"Declares InternalsVisibleTo project '{visibleTo}' that does not exist in the solution.");
                    continue;
                }

                string keyExpression = null;

                if (projectIsSigned)
                {
                    if (visibleToProject.ProducesLibraryNuGetPackage())
                    {
                        keyExpression = $"Key=\"$(NServiceBusKey)\" ";
                    }
                    else if (visibleToProject.IsTestProject())
                    {
                        keyExpression = $"Key=\"$(NServiceBusTestsKey)\" ";
                    }
                    else
                    {
                        throw new System.Exception("Project is signed but visible to project is not a package or a test project? What is going on?");
                    }
                }

                f.Fail($"Express in project file in an ItemGroup using '<InternalsVisibleTo Include=\"{visibleTo}\" {keyExpression}/>'");
            }
        }

        [Test]
        public void ProjectsShouldBeSignedWithCorrectKey()
        {
            new TestRunner("*.csproj", "Projects should be signed with the correct key")
                .Run(f =>
                {
                    var keyFile = f.XDocument.XPathSelectElement("/Project/PropertyGroup/AssemblyOriginatorKeyFile")?.Value;

                    if (keyFile is null)
                    {
                        return;
                    }

                    if (f.ProducesLibraryNuGetPackage())
                    {
                        var isSourcePackage = f.XDocument.XPathSelectElement("/Project/PropertyGroup/IncludeSourceFilesInPackage").GetBoolean() ?? false;

                        if (isSourcePackage)
                        {
                            // Missing concept: Source packages should generally not be signed, but for example in Core, PersistenceTests does need
                            // to be signed because it does (for some reason) need access to NServiceBus.Core internals. Checking that would involve
                            // also checking for InternalsVisibleTo in other projects which seems too complex for this test for now

                            //if (keyFile is not null)
                            //{
                            //    f.Fail("Source packages should not be signed. Remove the AssemblyOriginatorKeyFile element.");
                            //}
                        }
                        else if (keyFile is not @"..\NServiceBus.snk")
                        {
                            f.Fail(@"Projects creating NuGet packages should be signed with '<AssemblyOriginatorKeyFile>..\NServiceBus.snk</AssemblyOriginatorKeyFile>' and not use the $(SolutionDir) variable.");
                        }
                    }
                    else if (f.IsTestProject())
                    {
                        if (keyFile is not null and not @"..\NServiceBusTests.snk")
                        {
                            f.Fail(@"Test projects that need to be signed for InternalsVisibleTo should be signed with '<AssemblyOriginatorKeyFile>..\NServiceBusTests.snk</AssemblyOriginatorKeyFile>' and not use the $(SolutionDir) variable.");
                        }
                        else
                        {
                            var projectName = Path.GetFileNameWithoutExtension(f.FullPath);
                            bool needsSigning = projects.Any(proj =>
                            {
                                return proj.Value.XDocument.XPathSelectElements("/Project/ItemGroup/InternalsVisibleTo")
                                    .Any(ivt => string.Equals(ivt.Attribute("Include")?.Value, projectName, StringComparison.OrdinalIgnoreCase));
                            });

                            if (!needsSigning)
                            {
                                f.Fail($"Test project {projectName} is not used in an InternalsVisibleTo element in any other project and therefore should not be signed.");
                            }
                        }
                    }
                });
        }

        [Test]
        public void NoInternalsVisibleToForNonexistantProjects()
        {
            var projectNames = projects.Keys.Select(path => Path.GetFileNameWithoutExtension(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            new TestRunner("*.csproj", "No InternalsVisibleTo elements for projects that don't exist in the solution")
                .Run(f =>
                {
                    foreach (var element in f.XDocument.XPathSelectElements("/Project/ItemGroup/InternalsVisibleTo"))
                    {
                        var name = element.Attribute("Include").Value;
                        if (!projectNames.Contains(name))
                        {
                            f.Fail($"InternalsVisibleTo element for '{name}' does not match any project in the solution.");
                        }
                    }
                });
        }

        [Test]
        public void SortInternalsVisibleToItems()
        {
            new TestRunner("*.csproj", "InternalsVisibleTo elements should be sorted in alphabetical order")
                .Run(f =>
                {
                    var itemGroups = f.XDocument.XPathSelectElements("/Project/ItemGroup");
                    var isSorted = true;

                    foreach (var itemGroup in itemGroups)
                    {
                        var packageRefs = itemGroup.Descendants("InternalsVisibleTo")
                            .Select(pkgRef => pkgRef.Attribute("Include")?.Value)
                            .Where(name => name is not null)
                            .ToArray();

                        var sorted = packageRefs.OrderBy(packageRef => packageRef).ToArray();

                        isSorted = isSorted && Enumerable.Range(0, packageRefs.Length).All(i => packageRefs[i] == sorted[i]);
                    }

                    if (!isSorted)
                    {
                        f.Warn("InternalsVisibleTo elements should be sorted in alphabetical order within their parent ItemGroup");
                    }
                });
        }

        [GeneratedRegex(@"\[assembly: ?InternalsVisibleTo\(""([^""]+)""\)\]", RegexOptions.Compiled)]
        private static partial Regex InternalsVisibleToRegex();

        FileContext FindProjectForCodeFile(string codeFilePath)
        {
            var dirInfo = new DirectoryInfo(Path.GetDirectoryName(codeFilePath));

            while (dirInfo.FullName.Length >= TestSetup.RootDirectory.Length)
            {
                var file = dirInfo.GetFiles("*.csproj").FirstOrDefault();
                if (file is not null && projects.TryGetValue(file.FullName, out var project))
                {
                    return project;
                }

                dirInfo = dirInfo.Parent;
            }

            return null;
        }
    }
}
