using System.Text.Json;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Dark-theme tokens from governance graphical-profile JSON.</summary>
public sealed class GraphicalProfileTokens
{
    /// <summary>Canvas fill for repo banners.</summary>
    public string BackgroundDark { get; init; } = "#010D18";

    /// <summary>Accent stroke on banners.</summary>
    public string AccentDark { get; init; } = "#2FDFFF";

    /// <summary>Accent fill bar on banners.</summary>
    public string AccentFillDark { get; init; } = "#237CFF";

    /// <summary>Primary title text.</summary>
    public string TextDark { get; init; } = "#E6FBFF";

    /// <summary>Tagline text.</summary>
    public string MutedDark { get; init; } = "#2AA5FF";

    /// <summary>UI font stack.</summary>
    public string FontFamily { get; init; } = "Segoe UI, Candara, Calibri, sans-serif";

    /// <summary>Eyebrow size (matches Upgrade script: pageTitleSize).</summary>
    public int EyebrowSize { get; init; } = 28;

    /// <summary>Eyebrow weight.</summary>
    public string EyebrowWeight { get; init; } = "bold";

    /// <summary>Wordmark letter-spacing.</summary>
    public string WordmarkTracking { get; init; } = "2.2";

    /// <summary>Loads profile tokens from governance <c>profile.json</c>.</summary>
    public static GraphicalProfileTokens Load(string profilePath)
    {
        using var stream = File.OpenRead(profilePath);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var roles = root.GetProperty("roles");
        var typography = root.GetProperty("typography");

        return new GraphicalProfileTokens
        {
            BackgroundDark = roles.GetProperty("background").GetProperty("dark").GetString() ?? "#010D18",
            AccentDark = roles.GetProperty("accent").GetProperty("dark").GetString() ?? "#2FDFFF",
            AccentFillDark = roles.GetProperty("accentFill").GetProperty("dark").GetString() ?? "#237CFF",
            TextDark = roles.GetProperty("text").GetProperty("dark").GetString() ?? "#E6FBFF",
            MutedDark = roles.GetProperty("muted").GetProperty("dark").GetString() ?? "#2AA5FF",
            FontFamily = typography.GetProperty("fontFamily").GetString() ?? "Segoe UI, sans-serif",
            EyebrowSize = typography.GetProperty("pageTitleSize").GetInt32(),
            EyebrowWeight = typography.GetProperty("eyebrowWeight").GetString() ?? "bold",
            WordmarkTracking = ReadTracking(typography.GetProperty("wordmarkTracking")),
        };
    }

    private static string ReadTracking(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString() ?? "2.2",
            _ => "2.2",
        };
}
