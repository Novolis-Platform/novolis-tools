using System.Net;

namespace Novolis.Tools.Docs.Site;

/// <summary>Flat status marks for the docs home. Color comes from <c>currentColor</c>.</summary>
public static class OrgStatusMarks
{
    /// <summary>Inline mark. <paramref name="kind"/> is fail, cancel, ok, ship, package, nuget, or merge.</summary>
    public static string Svg(string kind)
    {
        var body = kind switch
        {
            "cancel" => Circle("""<path d="M4.5 8 H11.5" fill="none" stroke="var(--ngp-on-accent-fill)" stroke-width="1.6" stroke-linecap="square"/>"""),
            "ok" => Circle("""<path d="M4.2 8.2 L6.8 10.8 L11.8 5.2" fill="none" stroke="var(--ngp-on-accent-fill)" stroke-width="1.6" stroke-linecap="square" stroke-linejoin="miter"/>"""),
            "ship" => """<path d="M2.5 4.5 H8.8 L13 8 L8.8 11.5 H2.5 Z" fill="currentColor"/><circle cx="5.6" cy="8" r="1.1" fill="var(--ngp-on-accent-fill)"/>""",
            "package" or "nuget" => """<path d="M8 1.8 L14 5 V11 L8 14.2 L2 11 V5 Z" fill="currentColor"/><path d="M2 5 L8 8.2 L14 5 M8 8.2 V14.2" fill="none" stroke="var(--ngp-on-accent-fill)" stroke-width="1.2"/>""",
            "merge" => """<path d="M4 2.5 V6.5 C4 8.5 6.2 8.5 8 8.5 C9.8 8.5 12 8.5 12 6.5 V2.5 M8 8.5 V13.5" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="square"/><circle cx="4" cy="2.5" r="1.3" fill="currentColor"/><circle cx="12" cy="2.5" r="1.3" fill="currentColor"/><circle cx="8" cy="13.5" r="1.3" fill="currentColor"/>""",
            _ => Circle("""<path d="M5 5 L11 11 M11 5 L5 11" fill="none" stroke="var(--ngp-on-accent-fill)" stroke-width="1.6" stroke-linecap="square"/>"""),
        };
        return $"""<svg class="status-mark" viewBox="0 0 16 16" aria-hidden="true">{body}</svg>""";
    }

    /// <summary>Mark wrapped so the accessible name is on the parent and the color class is <c>mark-*</c>.</summary>
    public static string Chip(string kind, string label) =>
        $"""<span class="mark-{WebUtility.HtmlEncode(kind)}" title="{WebUtility.HtmlEncode(label)}">{Svg(kind)}<span class="visually-hidden">{WebUtility.HtmlEncode(label)}</span></span>""";

    private static string Circle(string glyph) =>
        $"""<circle cx="8" cy="8" r="7" fill="currentColor"/>{glyph}""";
}
