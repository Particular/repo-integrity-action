namespace RepoIntegrityTests.Infrastructure;

using NUnit.Framework;
using NUnit.Framework.Internal;

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

            var outputPath = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (outputPath is not null)
            {
                File.WriteAllText(outputPath, "has-warnings=true");
            }
        }
    }

    public static void Add(string testName, string[] warnings)
    {
        var testClass = TestContext.CurrentContext.Test.ClassName;
        var testMethodName = TestContext.CurrentContext.Test.MethodName;
        WriteLine($"**🟡 {testClass} : {testMethodName}**: {testName}");
        foreach (var warning in warnings)
        {
            WriteLine($"* {warning}");
        }
    }

    static void WriteLine(string message)
    {
        TestContext.Out.WriteLine("Got warning: " + message);
        Console.WriteLine("Direct to console: " + message);
        if (!isWriting)
        {
            return;
        }

        lock (padlock)
        {
            if (writer is null)
            {
                var path = Path.Combine(Environment.CurrentDirectory, "code-analysis-warnings.md");
                writer = new StreamWriter(path);
            }

            writer.WriteLine(message);
        }
    }
}