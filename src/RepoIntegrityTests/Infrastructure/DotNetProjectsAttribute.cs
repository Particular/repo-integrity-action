﻿namespace RepoIntegrityTests.Infrastructure
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

            if (!Directory.Exists(srcDirectory) || !Directory.GetFiles(srcDirectory, "*.sln").Any())
            {
                Assert.Ignore("Not a .NET project");
            }
        }
    }
}
