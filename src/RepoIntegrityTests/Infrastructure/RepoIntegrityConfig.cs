#nullable enable

namespace RepoIntegrityTests.Infrastructure;

using System.Text.RegularExpressions;

public class RepoIntegrityConfig
{
    public IgnoreRule[] Ignore { get; init; } = [];
}

public partial class IgnoreRule
{
    public required string Test { get; init; }
    public required string Path { get; init; }
    public string? Code { get; init; }

    Lazy<Regex> lazyRegex;

    public IgnoreRule()
    {
        lazyRegex = new(() =>
        {
            if (Path == "*")
            {
                return MatchAllRegex();
            }

            var regexPattern = "^" + Regex.Replace(Path!.Replace(".", @"\."), @"\*{1,2}", match => match.Value == "**" ? ".*" : "[^/]+") + "$";
            return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        });
    }

    public bool AppliesTo(string code, string path)
    {
        if (Code is null || Code == code)
        {
            return lazyRegex.Value.IsMatch(path);
        }

        return false;
    }


    [GeneratedRegex(".*", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MatchAllRegex();
}