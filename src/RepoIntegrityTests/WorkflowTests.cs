namespace RepoIntegrityTests
{
    using System.Linq;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    public class WorkflowTests
    {
        [Test]
        public void ShouldDefineDefaultShell()
        {
            new TestRunner("*.yml", "Workflows should set the default shell to 'pwsh' or 'bash' ensure same functionality on all platforms, unless there are no run steps")
                .Run(f =>
                {
                    if (!f.RelativePath.StartsWith(".github/workflows"))
                    {
                        return;
                    }

                    var workflow = new ActionsWorkflow(f.FullPath);

                    var workflowDefaultShell = workflow.Defaults.TryGetValue("run", out var run) && run.TryGetValue("shell", out var shell) ? shell : null;

                    if (workflowDefaultShell is "pwsh" or "bash")
                    {
                        return;
                    }

                    foreach (var job in workflow.Jobs)
                    {
                        var jobDefaultShell = job.Defaults.TryGetValue("run", out var jobRun) && jobRun.TryGetValue("shell", out var jobShell) ? jobShell : null;

                        if (jobDefaultShell is "pwsh" or "bash")
                        {
                            continue;
                        }

                        if (job.Steps.Any(step => step.Run is not null))
                        {
                            f.Fail($"Job '{job.Id}' does not have a default shell defined at the workflow or job level.");
                        }
                    }
                });
        }
    }
}
