using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Detects stub or placeholder docs pack files eligible for replacement.</summary>
public static partial class DocsPackThinDoc
{
    [GeneratedRegex("Reserved for future content", RegexOptions.IgnoreCase)]
    private static partial Regex ReservedRegex();

    [GeneratedRegex("dotnet add package Novolis\\.Example")]
    private static partial Regex ExamplePackageRegex();

    [GeneratedRegex("Canonical starter pack for new Novolis")]
    private static partial Regex CanonicalStarterRegex();

    [GeneratedRegex("novolis-template-dotnet/")]
    private static partial Regex TemplateDotnetRegex();

    /// <summary>Returns true when the file is missing, or thin and overwrite is enabled.</summary>
    public static bool ShouldReplace(string? path, DocsPackDocKind kind, string repoName, bool overwriteThin)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return true;
        }

        if (!overwriteThin)
        {
            return false;
        }

        var text = File.ReadAllText(path);
        var len = new FileInfo(path).Length;
        if (ReservedRegex().IsMatch(text))
        {
            return true;
        }

        if (len < 200)
        {
            return true;
        }

        if (kind == DocsPackDocKind.GettingStarted && ExamplePackageRegex().IsMatch(text))
        {
            return true;
        }

        if (kind == DocsPackDocKind.Readme &&
            !string.Equals(repoName, "novolis-template-dotnet", StringComparison.OrdinalIgnoreCase))
        {
            if (CanonicalStarterRegex().IsMatch(text) || TemplateDotnetRegex().IsMatch(text))
            {
                return true;
            }
        }

        return false;
    }
}
