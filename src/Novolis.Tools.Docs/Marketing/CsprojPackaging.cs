using System.Xml.Linq;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Reads IsPackable / PackageId from csproj and Directory.Build.props.</summary>
public static class CsprojPackaging
{
    /// <summary>Returns whether the project is packable (SDK default true, with name heuristics).</summary>
    public static bool GetEffectiveIsPackable(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var explicitValue = GetXmlText(doc, "IsPackable");
        if (string.Equals(explicitValue, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(explicitValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dir = Path.GetDirectoryName(csprojPath)!;
        while (!string.IsNullOrEmpty(dir))
        {
            var props = Path.Combine(dir, "Directory.Build.props");
            if (File.Exists(props))
            {
                var px = XDocument.Load(props);
                var v = GetXmlText(px, "IsPackable");
                if (string.Equals(v, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent) || parent == dir)
            {
                break;
            }

            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, ".gitignore")))
            {
                break;
            }

            dir = parent;
        }

        var leaf = Path.GetFileName(Path.GetDirectoryName(csprojPath)!);
        if (leaf is not null && System.Text.RegularExpressions.Regex.IsMatch(leaf, "(Lab|Studio|Play|App|Host|Smoke)$"))
        {
            return false;
        }

        return true;
    }

    /// <summary>Gets PackageId from the csproj, or the project folder name.</summary>
    public static string GetPackageId(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var id = GetXmlText(doc, "PackageId");
        if (!string.IsNullOrWhiteSpace(id))
        {
            return id.Trim();
        }

        return Path.GetFileNameWithoutExtension(csprojPath);
    }

    /// <summary>Gets Description from the csproj when present.</summary>
    public static string? GetDescription(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        return GetXmlText(doc, "Description");
    }

    private static string? GetXmlText(XDocument doc, string localName)
    {
        foreach (var pg in doc.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            var el = pg.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
            if (el?.Value is { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }
}
