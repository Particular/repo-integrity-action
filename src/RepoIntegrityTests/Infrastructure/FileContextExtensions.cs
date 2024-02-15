namespace RepoIntegrityTests
{
    using System.Linq;
    using System.Xml.XPath;
    using RepoIntegrityTests.Infrastructure;

    public static class FileContextExtensions
    {
        public static bool IsSdkProject(this FileContext file) =>
            file.XDocument.Root.Attribute("xmlns") is null;

        public static bool IsTestProject(this FileContext file) =>
            file.XDocument.XPathSelectElements("/Project/ItemGroup/PackageReference[@Include='Microsoft.NET.Test.Sdk']").Any();

        public static bool ProducesNuGetPackage(this FileContext file)
        {
            var doc = file.XDocument;

            var packAsTool = doc.XPathSelectElement("/Project/PropertyGroup/PackAsTool").GetBoolean();
            if (packAsTool ?? false)
            {
                return false;
            }

            var hasParticularPackaging = doc.XPathSelectElement("/Project/ItemGroup/PackageReference[@Include='Particular.Packaging']") is not null;
            var projectIsPackable = doc.XPathSelectElement("/Project/PropertyGroup/IsPackable").GetBoolean();

            var isPackable = projectIsPackable ?? GlobalFiles.CustomBuildProps.IsPackable ?? hasParticularPackaging;
            return isPackable;
        }
    }
}
