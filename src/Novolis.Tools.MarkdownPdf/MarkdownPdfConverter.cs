using Novolis.Markup.Markdown.Documents;

namespace Novolis.Tools.MarkdownPdf;

/// <summary>Converts Markdown files / strings to PDF using a named theme.</summary>
public static class MarkdownPdfConverter
{
    /// <summary>Reads <paramref name="inputPath"/> and writes a PDF to <paramref name="outputPath"/>.</summary>
    public static void ConvertFile(
        string inputPath,
        string outputPath,
        string themeId = MarkdownPdfThemes.DefaultId,
        MarkdownPdfConvertRequest? request = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Markdown file not found.", inputPath);

        var markdown = File.ReadAllText(inputPath);
        request ??= new MarkdownPdfConvertRequest();
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            request = new MarkdownPdfConvertRequest
            {
                Title = Path.GetFileNameWithoutExtension(inputPath),
                Author = request.Author,
                Subtitle = request.Subtitle,
                Series = request.Series,
                Rights = request.Rights,
                IncludeCover = request.IncludeCover,
                IncludeToc = request.IncludeToc,
            };
        }

        Convert(markdown, outputPath, themeId, request);
    }

    /// <summary>Converts Markdown text and writes a PDF file.</summary>
    public static void Convert(
        string markdown,
        string outputPath,
        string themeId = MarkdownPdfThemes.DefaultId,
        MarkdownPdfConvertRequest? request = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var bytes = ConvertToBytes(markdown, themeId, request);
        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(outputPath, bytes);
    }

    /// <summary>Converts Markdown text to PDF bytes.</summary>
    public static byte[] ConvertToBytes(
        string markdown,
        string themeId = MarkdownPdfThemes.DefaultId,
        MarkdownPdfConvertRequest? request = null)
    {
        var theme = MarkdownPdfThemes.Get(themeId);
        var options = theme.CreateOptions(request ?? new MarkdownPdfConvertRequest());
        return MarkdownDocumentPdfExporter.ExportToBytes(markdown ?? string.Empty, options);
    }
}
