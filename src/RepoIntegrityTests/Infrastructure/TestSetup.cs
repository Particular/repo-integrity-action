using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.All)]
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

namespace RepoIntegrityTests
{
    using Infrastructure;

    [SetUpFixture]
    public class TestSetup
    {
        public static string RootDirectory { get; private set; }
        public static string ActionRootPath { get; private set; }

        [OneTimeSetUp]
        public void SetupRootDirectories()
        {
            var currentDirectory = TestContext.CurrentContext.TestDirectory;
            ActionRootPath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", ".."));
            WarningReporter.Initialize();

#if DEBUG
            // For local testing, set to the path of a specific repo, or your whole projects directory, whatever works
            RootDirectory = @"/Users/david/Projects/NServiceBus";
#else
            RootDirectory = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
                ?? Environment.CurrentDirectory;
#endif
            Console.WriteLine($"RootDirectory = {RootDirectory}");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            WarningReporter.SaveReport();
        }
    }
}