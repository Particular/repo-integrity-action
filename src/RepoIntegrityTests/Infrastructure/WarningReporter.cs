namespace RepoIntegrityTests.Infrastructure;

using NUnit.Framework;

public static class WarningReporter
{
    static readonly object padlock = new();
    static StreamWriter writer;
    static bool isWriting;

    public static void Initialize()
    {
        Console.WriteLine("Initializing WarningReporter");
        writer?.Dispose();
        writer = null;
        var ci = Environment.GetEnvironmentVariable("CI");
        var stepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        isWriting = ci == "true" && stepSummary is not null;
    }

    public static void SaveReport()
    {
        Console.WriteLine("Finishing up...");
        if (writer is not null)
        {
            Console.WriteLine("Flushing writer");
            writer.Flush();
            writer.Dispose();
            writer = null;

            var outputPath = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            Console.WriteLine($"Saving output to {outputPath}");
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
        if (!isWriting)
        {
            return;
        }

        lock (padlock)
        {
            if (writer is null)
            {
                var path = Path.Combine(TestSetup.RootDirectory, "code-analysis-warnings.md");
                Console.WriteLine($"Creating writer: {path}");
                writer = new StreamWriter(path);
            }

            writer.WriteLine(message);
        }
    }
}