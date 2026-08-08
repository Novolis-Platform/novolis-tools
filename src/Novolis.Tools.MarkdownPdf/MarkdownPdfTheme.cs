using Novolis.Documents;
using Novolis.Markup.Markdown.Documents;
using Novolis.Math.Measure;

namespace Novolis.Tools.MarkdownPdf;

/// <summary>Named preset that builds <see cref="MarkdownPagedExportOptions"/>.</summary>
/// <param name="Id">Stable theme id (CLI <c>--theme</c>).</param>
/// <param name="DisplayName">Short label for <c>themes</c> listing.</param>
/// <param name="Description">One-line description.</param>
/// <param name="CreateOptions">Factory from convert request → export options.</param>
public sealed record MarkdownPdfTheme(
    string Id,
    string DisplayName,
    string Description,
    Func<MarkdownPdfConvertRequest, MarkdownPagedExportOptions> CreateOptions);

/// <summary>Inputs that themes combine with their presets.</summary>
public sealed class MarkdownPdfConvertRequest
{
    /// <summary>Document title (falls back to theme / inferred H1).</summary>
    public string? Title { get; init; }

    /// <summary>Author line.</summary>
    public string? Author { get; init; }

    /// <summary>Subtitle.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Series line.</summary>
    public string? Series { get; init; }

    /// <summary>Rights line.</summary>
    public string? Rights { get; init; }

    /// <summary>Emit a first/title page.</summary>
    public bool? IncludeCover { get; init; }

    /// <summary>Emit a TOC from level-1 headings.</summary>
    public bool? IncludeToc { get; init; }
}

/// <summary>Built-in Markdown → PDF themes.</summary>
public static class MarkdownPdfThemes
{
    /// <summary>Default theme id when none is specified.</summary>
    public const string DefaultId = "trade";

    /// <summary>All registered themes.</summary>
    public static IReadOnlyList<MarkdownPdfTheme> All { get; } =
    [
        new(
            "trade",
            "Trade",
            "6×9 in trade trim, chapter-title header, callout text box, page footer.",
            Trade),
        new(
            "report",
            "Report",
            "A4 report margins, document-title header, callout text box, page footer.",
            Report),
        new(
            "compact",
            "Compact",
            "A5, smaller type, tight spacing — notes and short briefs.",
            Compact),
        new(
            "plain",
            "Plain",
            "6×9, body only, page numbers, no running title header.",
            Plain),
    ];

    /// <summary>Looks up a theme by id (case-insensitive).</summary>
    public static MarkdownPdfTheme Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (var theme in All)
        {
            if (string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase))
                return theme;
        }

        var known = string.Join(", ", All.Select(static t => t.Id));
        throw new ArgumentException($"Unknown theme '{id}'. Known: {known}.", nameof(id));
    }

    /// <summary>Whether <paramref name="id"/> matches a registered theme.</summary>
    public static bool TryGet(string id, out MarkdownPdfTheme theme)
    {
        theme = null!;
        if (string.IsNullOrWhiteSpace(id))
            return false;
        foreach (var candidate in All)
        {
            if (!string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
                continue;
            theme = candidate;
            return true;
        }

        return false;
    }

    static MarkdownPagedExportOptions Trade(MarkdownPdfConvertRequest request) =>
        Base(request,
            trim: TrimPresets.Inch6x9,
            margin: TrimPresets.DefaultMargin,
            typography: new Typography
            {
                BodyFontSizePt = 11f,
                H1SizePt = 18f,
                H2SizePt = 13f,
                TableFontSizePt = 8.5f,
                LineHeight = 1.35f,
                AfterLevel1SpacingPt = 14f,
                ParagraphSpacingPt = 8f,
            },
            headerTemplate: "{chapter}",
            useChapterTitleHeader: true,
            includeCover: request.IncludeCover ?? false,
            includeToc: request.IncludeToc ?? false);

    static MarkdownPagedExportOptions Report(MarkdownPdfConvertRequest request) =>
        Base(request,
            trim: TrimPresets.A4,
            margin: TrimPresets.ReportMargin,
            typography: new Typography
            {
                BodyFontSizePt = 10.5f,
                H1SizePt = 16f,
                H2SizePt = 12f,
                TableFontSizePt = 9f,
                LineHeight = 1.3f,
                AfterLevel1SpacingPt = 12f,
                ParagraphSpacingPt = 6f,
            },
            headerTemplate: "{title}",
            useChapterTitleHeader: false,
            includeCover: request.IncludeCover ?? false,
            includeToc: request.IncludeToc ?? false);

    static MarkdownPagedExportOptions Compact(MarkdownPdfConvertRequest request) =>
        Base(request,
            trim: TrimPresets.A5,
            margin: TrimPresets.ReportMargin,
            typography: new Typography
            {
                BodyFontSizePt = 9.5f,
                H1SizePt = 14f,
                H2SizePt = 11f,
                TableFontSizePt = 8f,
                LineHeight = 1.25f,
                AfterLevel1SpacingPt = 10f,
                ParagraphSpacingPt = 5f,
            },
            headerTemplate: "{title}",
            useChapterTitleHeader: true,
            includeCover: request.IncludeCover ?? false,
            includeToc: request.IncludeToc ?? false);

    static MarkdownPagedExportOptions Plain(MarkdownPdfConvertRequest request) =>
        new()
        {
            Title = request.Title,
            Author = request.Author,
            Subtitle = request.Subtitle,
            Series = request.Series,
            Rights = request.Rights,
            IncludeCover = request.IncludeCover ?? false,
            IncludeToc = request.IncludeToc ?? false,
            Trim = TrimPresets.Inch6x9,
            Margin = TrimPresets.DefaultMargin,
            HeaderTemplate = string.Empty,
            UseChapterTitleHeader = false,
            FooterTemplate = "{page}",
            Typography = new Typography
            {
                BodyFontSizePt = 11f,
                H1SizePt = 16f,
                LineHeight = 1.35f,
            },
            TextBox = ReaderTextBox(),
        };

    static MarkdownPagedExportOptions Base(
        MarkdownPdfConvertRequest request,
        Size trim,
        Thickness margin,
        Typography typography,
        string headerTemplate,
        bool useChapterTitleHeader,
        bool includeCover,
        bool includeToc) =>
        new()
        {
            Title = request.Title,
            Author = request.Author,
            Subtitle = request.Subtitle,
            Series = request.Series,
            Rights = request.Rights,
            IncludeCover = includeCover,
            IncludeToc = includeToc,
            Trim = trim,
            Margin = margin,
            Typography = typography,
            HeaderTemplate = headerTemplate,
            UseChapterTitleHeader = useChapterTitleHeader,
            FooterTemplate = "{page}",
            TextBox = ReaderTextBox(),
        };

    static TextBoxBlock ReaderTextBox() => new()
    {
        PaddingPt = 6f,
        BorderStrokePt = 0.8f,
        BorderColor = DocumentColor.Gray,
        Background = DocumentColor.LightGray,
        FontSizePt = 8.5f,
        LineHeight = 1.22f,
        LineGapPt = 1.5f,
        TextColor = DocumentColor.Gray,
    };
}
