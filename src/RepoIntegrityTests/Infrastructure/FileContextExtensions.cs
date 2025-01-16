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

        public static bool ProducesLibraryNuGetPackage(this FileContext file)
        {
            var doc = file.XDocument;

            var packAsTool = doc.XPathSelectElement("/Project/PropertyGroup/PackAsTool").GetBoolean();
            if (packAsTool ?? false)
            {
                return false;
            }

            var hasParticularPackaging = doc.XPathSelectElement("/Project/ItemGroup/PackageReference[@Include='Particular.Packaging']") is not null;
            var projectIsPackable = doc.XPathSelectElement("/Project/PropertyGroup/IsPackable").GetBoolean();

            // This isn't taking into consideration whether Particular.Packaging is in the Custom.Build.props, which it is
            // in ServiceControl, but then only the platform sample project is enabled.
            var isPackable = projectIsPackable ?? GlobalFiles.CustomBuildProps.IsPackable ?? hasParticularPackaging;
            return isPackable;
        }

        public static bool ProducesSourcePackage(this FileContext file)
        {
            var doc = file.XDocument;

            var includeBuildOutputElement = doc.XPathSelectElement("/Project/PropertyGroup/IncludeBuildOutput");
            var includeSourceFilesInPackageElement = doc.XPathSelectElement("/Project/PropertyGroup/IncludeSourceFilesInPackage");

            return includeBuildOutputElement is not null
                   && includeSourceFilesInPackageElement is not null
                   && bool.TryParse(includeSourceFilesInPackageElement.Value, out var includeSourceFilesInPackage) && includeSourceFilesInPackage
                   && bool.TryParse(includeBuildOutputElement.Value, out var includeBuildOutput) && !includeBuildOutput;
        }
    }
}
