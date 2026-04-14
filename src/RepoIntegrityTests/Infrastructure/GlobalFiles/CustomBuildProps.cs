namespace RepoIntegrityTests;

using System.Xml.Linq;
using System.Xml.XPath;

public class CustomBuildProps
{
    public XDocument Document { get; }
    public bool Loaded => Document is not null;
    public bool? IsPackable { get; }

    public CustomBuildProps()
    {
        var customBuildPropsPath = Path.Combine(TestSetup.RootDirectory, "src", "Custom.Build.props");
        if (!File.Exists(customBuildPropsPath))
        {
            return;
        }

        Document = XDocument.Load(customBuildPropsPath);
        IsPackable = Document.XPathSelectElement("/Project/PropertyGroup/IsPackable").GetBoolean();
    }
}
