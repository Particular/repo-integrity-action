namespace RepoIntegrityTests;

using System.Xml.Linq;
using System.Xml.XPath;
using Infrastructure;
using NUnit.Framework;

[DotNetProjects]
public class GeneratePathProperty
{
    [Test]
    public void IsCorrectlyImplemented()
    {
        new TestRunner("*.csproj", "Compile Remove elements with wildcards are set up correctly")
            .SdkProjects()
            .Run(f =>
            {
                var pkgVariables = f.XDocument
                    .XPathSelectElements("/Project/ItemGroup/PackageReference[@GeneratePathProperty='true']")
                    .Select(el => el.Attribute("Include").Value)
                    .Select(name => $"$(Pkg{name.Replace(".", "_")})")
                    .ToArray();

                var pkgVariablesAsConditions = pkgVariables
                    .Select(pkgVar => $"'{pkgVar}' != ''")
                    .ToArray();

                var removesWithWildcard = f.XDocument.XPathSelectElements("/Project/ItemGroup/Compile[@Remove]")
                    .Select(el => new
                    {
                        Element = el,
                        ItemGroup = el.Parent,
                        RemoveValue = el.Attribute("Remove")?.Value
                    })
                    .Where(x => x.RemoveValue.Contains("**"))
                    .ToArray();

                foreach (var remove in removesWithWildcard)
                {
                    if (!RemovalIsCorrect(remove.Element, remove.RemoveValue, pkgVariables))
                    {
                        f.Fail($"Compile.Remove='{remove.RemoveValue}' must contain a variable like `$(PkgNServiceBus_AcceptanceTests_Source)` from a PackageReference that contains GeneratePathProperty=\"true\"");
                    }

                    var compileCondition = remove.Element.Attribute("Condition")?.Value;
                    var itemGroupCondition = remove.Element.Parent.Attribute("Condition")?.Value;

                    if (!ConditionIsCorrect(compileCondition, pkgVariablesAsConditions) && !ConditionIsCorrect(itemGroupCondition, pkgVariablesAsConditions))
                    {
                        f.Fail($"Compile.Remove='{remove.RemoveValue}' must have a condition like Condition=\"'$(PkgNServiceBus_AcceptanceTests_Sources)' != ''\" on either the Compile element or the parent ItemGroup element");
                    }
                }
            });

        bool RemovalIsCorrect(XElement compileElement, string removePath, string[] variables)
        {
            foreach (var variable in variables)
            {
                if (removePath.Contains(variable))
                {
                    return true;
                }
            }

            return false;
        }

        bool ConditionIsCorrect(string condition, string[] pkgVariablesAsConditions)
            => pkgVariablesAsConditions.Contains(condition);
    }
}