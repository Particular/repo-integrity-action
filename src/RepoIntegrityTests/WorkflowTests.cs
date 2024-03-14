namespace RepoIntegrityTests
{
    using System.Linq;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    public class WorkflowTests
    {
        [Test]
        public void ShouldHavePwshDefault()
        {
            new TestRunner("*.yml", "Workflows should set the default shell to pwsh to ensure same functionality on all platforms, unless there are no run steps")
                .Run(f =>
                {
                    if (!f.RelativePath.StartsWith(".github/workflows"))
                    {
                        return;
                    }

                    var workflow = new ActionsWorkflow(f.FullPath);

                    var defaultShellSet = workflow.Defaults.TryGetValue("run", out var run) && run.TryGetValue("shell", out var shell) && shell == "pwsh";

                    if (!defaultShellSet)
                    {
                        if (workflow.Jobs.SelectMany(job => job.Steps).Any(step => step.Run is not null))
                        {
                            f.Fail();
                        }
                    }
                });
        }
    }
}
