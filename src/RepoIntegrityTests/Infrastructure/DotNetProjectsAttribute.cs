namespace RepoIntegrityTests.Infrastructure
{
    using System;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using NUnit.Framework.Internal;

    public class DotNetProjectsAttribute : Attribute, IApplyToContext
    {
        public void ApplyToContext(TestExecutionContext context)
        {
            var srcDirectory = Path.Combine(TestSetup.RootDirectory, "src");

            if (Directory.Exists(srcDirectory))
            {
                var isDotNet = Directory.GetFiles(srcDirectory, "*.slnx").Any()
                               || Directory.GetFiles(srcDirectory, "*.sln").Any();

                if (isDotNet)
                {
                    return;
                }
            }

            Assert.Ignore("Not a .NET project");
        }
    }
}
