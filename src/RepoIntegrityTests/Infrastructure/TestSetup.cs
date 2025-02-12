using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.All)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

namespace RepoIntegrityTests
{
    using System.Text.RegularExpressions;
    using Infrastructure;

    [SetUpFixture]
    public class TestSetup
    {
        public static string RootDirectory { get; private set; }
        public static string ActionRootPath { get; private set; }
        public static bool IsPrivateRepo { get; private set; }

        static Dictionary<string, IgnoreRule[]> ignoreRules = new(StringComparer.OrdinalIgnoreCase);

        [OneTimeSetUp]
        public void SetupRootDirectories()
        {
            var currentDirectory = TestContext.CurrentContext.TestDirectory;
            ActionRootPath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", ".."));
            WarningReporter.Initialize();

#if DEBUG
            // For local testing, set to the path of a specific repo, or your whole projects directory, whatever works
            RootDirectory = @"/Users/david/Projects/NServiceBus.AwsLambda.Sqs";
#else
            RootDirectory = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
                ?? Environment.CurrentDirectory;
#endif
            Console.WriteLine($"RootDirectory = {RootDirectory}");

            var configPath = Path.Combine(RootDirectory, ".repointegrity.yml");
            if (File.Exists(configPath))
            {
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithCaseInsensitivePropertyMatching()
                    .Build();

                var config = deserializer.Deserialize<RepoIntegrityConfig>(File.ReadAllText(configPath));
                ignoreRules = config.Ignore.GroupBy(r => r.Test, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            }

            IsPrivateRepo = Environment.GetEnvironmentVariable("PRIVATE_REPO")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public static bool ShouldExclude(string testMethodName, string code, string relativePath)
        {
            if (ignoreRules.TryGetValue(testMethodName, out var ruleList))
            {
                foreach (var rule in ruleList)
                {
                    if (rule.AppliesTo(code, relativePath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            WarningReporter.SaveReport();
        }

        record Exclusion(string TestName, Regex AppliesTo);
    }
}