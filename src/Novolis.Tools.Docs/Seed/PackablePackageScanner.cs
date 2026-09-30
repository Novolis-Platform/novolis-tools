using System.Xml.Linq;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Lists packable Novolis.* package IDs under src/ and codegen/.</summary>
public static class PackablePackageScanner
{
    /// <summary>Scans the repository for packable Novolis.* package IDs.</summary>
    public static IReadOnlyList<string> Scan(string repoRoot)
    {
        var pkgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dirName in new[] { "src", "codegen" })
        {
            var root = Path.Combine(repoRoot, dirName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            {
                var doc = XDocument.Load(csproj);
                var id = GetPackageId(doc, csproj);
                if (!id.StartsWith("Novolis.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (IsPackable(doc) && pkgs.Add(id))
                {
                    // tracked in set
                }
            }
        }

        return pkgs.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsPackable(XDocument doc)
    {
        foreach (var pg in doc.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            var el = pg.Elements().FirstOrDefault(e => e.Name.LocalName == "IsPackable");
            if (el?.Value.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetPackageId(XDocument doc, string csprojPath)
    {
        foreach (var pg in doc.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            var el = pg.Elements().FirstOrDefault(e => e.Name.LocalName == "PackageId");
            if (el?.Value is { Length: > 0 } id)
            {
                return id.Trim();
            }
        }

        return Path.GetFileNameWithoutExtension(csprojPath);
    }
}
