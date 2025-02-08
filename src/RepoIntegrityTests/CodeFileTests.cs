namespace RepoIntegrityTests
{
    using NUnit.Framework;
    using RepoIntegrityTests.Infrastructure;

    [DotNetProjects]
    public class CodeFileTests
    {
        [Test]
        public void GlobalSuppressionsInEditorConfig()
        {
            new TestRunner("GlobalSuppressions.cs", "Global suppressions should be expressed in .editorconfig files so they are easily findable", failIfNoMatches: false)
                .Run(f => f.Fail());
        }
    }
}
