using System.Globalization;

namespace Novolis.Tools.Docs.Org;

/// <summary>Picks the highest numeric dotted version.</summary>
public static class OrgVersions
{
    /// <summary>Returns the highest version, ignoring blanks.</summary>
    public static string Latest(IEnumerable<string?> versions)
    {
        string? best = null;
        foreach (var version in versions)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            if (best is null || Compare(version, best) > 0)
            {
                best = version;
            }
        }

        return best ?? "";
    }

    /// <summary>Compares dotted numeric versions. Non-numeric parts count as zero.</summary>
    public static int Compare(string left, string right)
    {
        var a = left.Split('.');
        var b = right.Split('.');
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var ai = i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0;
            var bi = i < b.Length && int.TryParse(b[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ? y : 0;
            var cmp = ai.CompareTo(bi);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}
