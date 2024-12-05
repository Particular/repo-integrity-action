namespace RepoIntegrityTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml.XPath;
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
            new TestRunner("InternalsVisibleTo.cs", "InternalsVisibleTo should be registered in project files", failIfNoMatches: false)
                .Run(f =>
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
                });
        }

        [Test]
        public void ProjectsShouldBeSignedWithCorrectKey() => new TestRunner("*.csproj", "Projects should be signed with the correct key")
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
                        else if (keyFile is not "$(SolutionDir)NServiceBus.snk" and not @"..\NServiceBus.snk")
                        {
                            f.Fail("Projects creating NuGet packages should be signed with '<AssemblyOriginatorKeyFile>$(SolutionDir)NServiceBus.snk</AssemblyOriginatorKeyFile>'");
                        }
                    }
                    else if (f.IsTestProject() && keyFile is not null)
                    {
                        if (keyFile is not "$(SolutionDir)NServiceBusTests.snk" and not @"..\NServiceBusTests.snk")
                        {
                            f.Fail("Test projects that need to be signed for InternalsVisibleTo should be signed with '<AssemblyOriginatorKeyFile>$(SolutionDir)NServiceBusTests.snk</AssemblyOriginatorKeyFile>'");
                        }
                    }
                });

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

                dirInfo = file.Directory;
            }

            return null;
        }
    }
}
