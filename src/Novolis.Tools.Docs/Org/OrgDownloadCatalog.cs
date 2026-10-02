using System.Globalization;
using System.Text;

namespace Novolis.Tools.Docs.Org;

/// <summary>Groups shipped release assets into per-app download rows.</summary>
public static class OrgDownloadCatalog
{
    /// <summary>One shipped release and the apps parsed from its assets.</summary>
    public static IReadOnlyList<OrgDownloadRelease> From(IReadOnlyList<OrgStatusRelease> releases)
    {
        var result = new List<OrgDownloadRelease>();
        foreach (var release in releases)
        {
            var apps = new Dictionary<string, OrgDownloadApp>(StringComparer.OrdinalIgnoreCase);
            var checksums = new List<OrgStatusAsset>();
            foreach (var asset in release.Assets)
            {
                if (string.IsNullOrWhiteSpace(asset.Name) || string.IsNullOrWhiteSpace(asset.Url))
                {
                    continue;
                }

                if (IsChecksum(asset.Name))
                {
                    checksums.Add(asset);
                    continue;
                }

                var stem = AppStem(asset.Name);
                if (!apps.TryGetValue(stem, out var app))
                {
                    app = new OrgDownloadApp { Name = DisplayName(stem) };
                    apps[stem] = app;
                }

                Slot(app, asset.Name).Add(asset);
            }

            result.Add(new OrgDownloadRelease
            {
                Repo = release.Repo,
                Tag = release.Tag,
                Url = release.Url,
                Published = release.Published,
                Apps = apps.Values.OrderBy(static app => app.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                Checksums = checksums,
            });
        }

        return result;
    }

    /// <summary>Inserts spaces into a PascalCase stem. Digit suffixes such as 3D stay together.</summary>
    public static string DisplayName(string stem)
    {
        if (string.IsNullOrEmpty(stem))
        {
            return "";
        }

        var sb = new StringBuilder(stem.Length + 8);
        sb.Append(stem[0]);
        for (var i = 1; i < stem.Length; i++)
        {
            var current = stem[i];
            var previous = stem[i - 1];
            var next = i + 1 < stem.Length ? stem[i + 1] : '\0';
            var boundary =
                (char.IsLower(previous) && char.IsUpper(current))
                || (char.IsLetter(previous) && char.IsDigit(current))
                || (char.IsUpper(previous) && char.IsUpper(current) && char.IsLower(next));
            if (boundary)
            {
                sb.Append(' ');
            }

            sb.Append(current);
        }

        return sb.ToString();
    }

    /// <summary>Short size label. Zero or negative sizes are omitted.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "";
        }

        const double kb = 1024d;
        const double mb = 1024d * 1024d;
        if (bytes < mb)
        {
            return (bytes / kb).ToString("0.#", CultureInfo.InvariantCulture) + " KB";
        }

        return (bytes / mb).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
    }

    private static bool IsChecksum(string name) =>
        name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase);

    private static string AppStem(string name)
    {
        var dash = name.IndexOf('-');
        var stem = dash > 0 ? name[..dash] : name;
        const string setup = "Setup";
        if (stem.EndsWith(setup, StringComparison.OrdinalIgnoreCase) && stem.Length > setup.Length)
        {
            stem = stem[..^setup.Length];
        }

        return stem;
    }

    private static List<OrgStatusAsset> Slot(OrgDownloadApp app, string name)
    {
        if (name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-android", StringComparison.OrdinalIgnoreCase))
        {
            return app.Android;
        }

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-win-", StringComparison.OrdinalIgnoreCase))
        {
            return app.Windows;
        }

        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || name.Contains("-linux-", StringComparison.OrdinalIgnoreCase))
        {
            return app.Linux;
        }

        return app.Other;
    }
}
