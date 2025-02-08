namespace RepoIntegrityTests.Infrastructure;

using NUnit.Framework;

public static class WarningReporter
{
    static readonly object padlock = new();
    static StreamWriter writer;
    static bool isWriting;

    public static void Initialize()
    {
        writer?.Dispose();
        writer = null;
        var ci = Environment.GetEnvironmentVariable("CI");
        var stepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        isWriting = ci == "true" && stepSummary is not null;
    }

    public static void SaveReport()
    {
        if (writer is not null)
        {
            writer.Flush();
            writer.Dispose();
            writer = null;
        }
    }

    public static void Add(string testName, string[] warnings)
    {
        var testClass = TestContext.CurrentContext.Test.ClassName;
        var testMethodName = TestContext.CurrentContext.Test.MethodName;
        WriteLine();
        WriteLine($"**🟡 {testClass} : {testMethodName}**: {testName}");
        WriteLine();
        foreach (var warning in warnings)
        {
            WriteLine($"* {warning}");
        }
    }

    static void WriteLine(string message = null)
    {
        if (!isWriting)
        {
            return;
        }

        lock (padlock)
        {
            if (writer is null)
            {
                var path = Path.Combine(TestSetup.RootDirectory, "code-analysis-warnings.md");
                writer = new StreamWriter(path);
            }

            writer.WriteLine(message);
        }
    }
}