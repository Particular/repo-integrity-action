namespace RepoIntegrityTests.Infrastructure
{
    using System.Xml.Linq;

    public static class XDocumentExtensions
    {
        public static bool? GetBoolean(this XElement element)
        {
            if (bool.TryParse(element?.Value, out var value))
            {
                return value;
            }

            return null;
        }
    }
}
