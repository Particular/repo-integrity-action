namespace RepoIntegrityTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using NUnit.Framework;

    public class FileContext(string filePath)
    {
        public string FullPath { get; } = filePath;
        public string DirectoryPath { get; } = Path.GetDirectoryName(filePath);
        public string FileName { get; } = Path.GetFileName(filePath);
        public string RelativePath { get; } = filePath.Substring(TestSetup.RootDirectory.Length + 1).Replace("\\", "/");
        public bool IsFailed { get; private set; }
        public bool HasWarnings { get; private set; }

        public List<string> FailReasons { get; } = [];
        public List<string> WarningReasons { get; } = [];

        public void Fail(string reason = null, string code = null)
        {
            if (TestSetup.ShouldExclude(TestContext.CurrentContext.Test.MethodName, code, RelativePath))
            {
                return;
            }

            IsFailed = true;
            if (reason is not null)
            {
                if (code is not null)
                {
                    reason = $"(Code: {code}) {reason}";
                }
                FailReasons.Add(reason);
            }
        }

        public void Warn(string reason = null, string code = null)
        {
            if (TestSetup.ShouldExclude(TestContext.CurrentContext.Test.MethodName, code, RelativePath))
            {
                return;
            }

            HasWarnings = true;
            if (reason is not null)
            {
                if (code is not null)
                {
                    reason = $"(Code: {code}) {reason}";
                }
                WarningReasons.Add(reason);
            }
        }

        Lazy<XDocument> xdoc = new Lazy<XDocument>(() => XDocument.Load(filePath), false);
        public XDocument XDocument => xdoc.Value;

        public override string ToString()
        {
            if (!IsFailed)
            {
                return "OK";
            }

            return FailReasons.Count switch
            {
                0 => "Failed: (no reason)",
                1 => $"Failed: {FailReasons.First()}",
                _ => $"Failed: {FailReasons.Count} reasons"
            };
        }
    }
}