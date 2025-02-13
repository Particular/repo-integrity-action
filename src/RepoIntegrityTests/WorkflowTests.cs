namespace RepoIntegrityTests
{
    using System.Linq;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    public partial class WorkflowTests
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

        [Test]
        public void CheckForSecrets()
        {
            if (TestSetup.IsPrivateRepo)
            {
                return;
            }

            new TestRunner("ci.yml", "Early check for secrets when CI workflow uses secrets", failIfNoMatches: false)
                .Run(f =>
                {
                    if (!f.RelativePath.StartsWith(".github/workflows"))
                    {
                        return;
                    }

                    var contents = File.ReadAllText(f.FullPath);
                    var secretMatches = SecretsRegex().Matches(contents);
                    if (!secretMatches.Any())
                    {
                        // No secrets and also no Check for Secrets, all good
                        return;
                    }

                    var secretNames = secretMatches.OfType<Match>()
                        .Select(m => m.Groups["Name"].Value)
                        .Distinct()
                        .ToArray();

                    if (secretNames.Length == 1 && secretNames[0] == "SECRETS_AVAILABLE")
                    {
                        f.Fail("ci.yml does not need a check for secrets using ${{ secrets.SECRETS_AVAILABLE }} if it doesn't use any other secrets. ");
                        return;
                    }

                    var workflow = new ActionsWorkflow(f.FullPath);

                    foreach (var job in workflow.Jobs)
                    {
                        var firstStep = job.Steps.FirstOrDefault();
                        if (firstStep is not null)
                        {
                            if (firstStep.Run is null || firstStep.Run.Contains("secrets.SECRETS_AVAILABLE"))
                            {
                                f.Fail($"Job '{job.Id}' is part of a workflow that uses secrets but does not check for presence of secrets as a first step. See https://github.com/Particular/Platform/blob/main/guidelines/github-actions/annotated-workflows.md#secrets");
                            }
                        }
                    }
                });
        }

        [GeneratedRegex(@"\$\{\{\s+secrets\.(?<Name>\w+)", RegexOptions.Compiled)]
        private partial Regex SecretsRegex();
    }
}
