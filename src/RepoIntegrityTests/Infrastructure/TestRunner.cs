﻿namespace RepoIntegrityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    public class TestRunner
    {
        readonly string name;
        IEnumerable<FileContext> files;

        public TestRunner(string glob, string name, bool failIfNoMatches = true)
        {
            this.name = name;
            var filesArray = Directory.GetFiles(TestSetup.RootDirectory, glob, SearchOption.AllDirectories)
                .Select(filePath => new FileContext(filePath))
                .ToArray();

            if (failIfNoMatches && filesArray.Length == 0)
            {
                Assert.Fail($"No files found matching '{glob}'.");
            }

            // If this isn't materialized into an array first some weird multiple enumeration things start to happen
            files = filesArray;
        }

        public TestRunner IgnoreRegex(string pattern, RegexOptions regexOptions = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pattern = pattern.Replace("\\\\", "/");
            }

            files = files.Where(f => !Regex.IsMatch(f.FullPath, pattern, regexOptions));

            return this;
        }

        public TestRunner IgnoreWildcard(string wildcardExpression)
        {
            var pattern = Regex.Escape(wildcardExpression).Replace(@"\*", ".*").Replace(@"\?", ".");
            return IgnoreRegex(pattern);
        }

        public TestRunner SdkProjects()
        {
            files = files.Where(f => f.IsSdkProject());
            return this;
        }

        public TestRunner TestProjects()
        {
            files = files.Where(f => f.IsTestProject());
            return this;
        }

        public TestRunner ProjectsProducingNuGetPackages()
        {
            files = files.Where(f => f.ProducesNuGetPackage());
            return this;
        }

        public void Run(Action<FileContext> testAction)
        {
            var results = files.ForEach(testAction)
                .Where(f => f.IsFailed)
                .SelectMany(f =>
                {
                    if (!f.FailReasons.Any())
                    {
                        return [f.RelativePath];
                    }

                    return f.FailReasons.Select(reason => $"{f.RelativePath} - {reason}");
                })
                .ToArray();

            if (results.Any())
            {
                Assert.Fail($"{name}:\r\n  > {string.Join("\r\n  > ", results)}");
            };
        }
    }
}