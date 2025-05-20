namespace RepoIntegrityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.XPath;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    [DotNetProjects]
    public partial class TestProjects
    {
        [Test]
        public void ValidateProjectFrameworks()
        {
            var ciPath = Path.Combine(TestSetup.RootDirectory, ".github", "workflows", "ci.yml");
            if (!File.Exists(ciPath))
            {
                Assert.Ignore("No ci.yml workflow found in root directory");
            }

            var workflow = new ActionsWorkflow(ciPath);

            var explicitNetVersionsRequested = workflow.Jobs
                .SelectMany(j => j.Steps.Where(s => s.Uses?.StartsWith("actions/setup-dotnet@") ?? false))
                .Select(step =>
                {
                    var dotnetVersionsAtt = step.With.GetValueOrDefault("dotnet-version");

                    var useGlobalJsonAt = step.With.GetValueOrDefault("global-json-file");
                    if (useGlobalJsonAt is not null)
                    {
                        var globalJsonPath = Path.Combine(TestSetup.RootDirectory, useGlobalJsonAt);
                        if (File.Exists(globalJsonPath))
                        {
                            // Simplistic parsing
                            var globalJson = File.ReadAllText(globalJsonPath);
                            var versionMatch = Regex.Match(globalJson, @"""version"": ""(\d+\.\d+)\.\d+""", RegexOptions.IgnoreCase);
                            if (versionMatch.Success)
                            {
                                var majorMinor = versionMatch.Groups[1].Value;
                                return $"{majorMinor}.x" + Environment.NewLine + dotnetVersionsAtt;
                            }
                        }
                    }

                    return dotnetVersionsAtt;
                })
                .Where(versions => versions is not null)
                .Select(versions => Regex.Split(versions, @"(\r?\n)+").Where(s => !string.IsNullOrWhiteSpace(s)).ToArray())
                .ToArray();

            // If workflow has more than one job, make sure someone didn't update one setup-dotnet and forget the other one
            for (var i = 1; i < explicitNetVersionsRequested.Length; i++)
            {
                Assert.That(explicitNetVersionsRequested[0], Is.EquivalentTo(explicitNetVersionsRequested[i]), "All the .NET versions requested by jobs in ci.yml should be the same");
            }

            // Empty if nothing in workflow file, could mean all tests are net4xx
            var expectedFrameworks = explicitNetVersionsRequested.FirstOrDefault()?.Select(DotNetVersionToTargetFramework).ToArray() ?? [];

            var collectedTestFrameworks = new List<(string path, string frameworks)>();

            Console.WriteLine("Expected expectedFrameworks: " + string.Join(", ", expectedFrameworks));

            new TestRunner("*.csproj", "Find tests")
                .SdkProjects()
                .TestProjects()
                .Run(file =>
                {
                    var frameworksText = file.XDocument.XPathSelectElement("/Project/PropertyGroup/TargetFramework")?.Value
                        ?? file.XDocument.XPathSelectElement("/Project/PropertyGroup/TargetFrameworks")?.Value;

                    var frameworks = frameworksText.Split(';')
                        .Select(tfm => tfm.Contains('-') ? tfm.Split('-')[0] : tfm)
                        .ToArray();

                    var nonNetFxFrameworks = frameworks.Where(f => !f.StartsWith("net4")).ToArray();

                    collectedTestFrameworks.Add((file.FullPath, string.Join(';', nonNetFxFrameworks)));

                    if (expectedFrameworks is null && frameworks.All(tfm => tfm.StartsWith("net4")))
                    {
                        // net4xx TFM doesn't need a dotnet-setup
                        return;
                    }

                    var nonNetfxFrameworks = frameworks.Where(tfm => !tfm.StartsWith("net4")).ToArray();

                    if (!nonNetfxFrameworks.All(tfm => expectedFrameworks.Contains(tfm)))
                    {
                        file.Fail("Target frameworks don't match the dotnet-versions in the ci.yml workflow");
                    }
                });

            var groups = collectedTestFrameworks
                .Where(x => !string.IsNullOrEmpty(x.frameworks))
                .GroupBy(x => x.frameworks)
                .OrderBy(g => g.Count())
                .ToArray();

            if (groups.Length > 1)
            {
                var msg = new StringBuilder().AppendLine("The target frameworks of the test projects do not all match:");

                foreach (var g in groups)
                {
                    msg.AppendLine($"  * Target Frameworks: '{g.Key}':");
                    foreach (var proj in g)
                    {
                        msg.AppendLine($"    * {proj.path}");
                    }
                }

                Assert.Fail(msg.ToString());
            }
        }

        string DotNetVersionToTargetFramework(string dotnetVersion)
        {
            var result = dotnetVersion switch
            {
                "2.1.x" => "netcoreapp2.1",
                "3.1.x" => "netcoreapp3.1",
                _ => null
            };

            if (result != null)
            {
                return result;
            }

            var match = DotNetVersionRegex().Match(dotnetVersion);
            if (match.Success)
            {
                var dotnetMajor = match.Groups[1].Value;
                return $"net{dotnetMajor}.0";
            }

            throw new Exception($"Unable to map dotnet-version value (i.e. '8.0.x') to target framework (i.e. 'net8.0'. A mapping for the value '{dotnetVersion}' may be missing, or it may be incorrect.");
        }

        [GeneratedRegex(@"(\d+)\.0\.x")]
        private static partial Regex DotNetVersionRegex();
    }
}
