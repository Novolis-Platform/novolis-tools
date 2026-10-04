using System.Text.RegularExpressions;
using Jint;

namespace Novolis.Tools.CanvasHtml;

/// <summary>Renders a Cursor <c>.canvas.tsx</c> source to self-contained HTML.</summary>
public static class CanvasHtmlRenderer
{
    /// <summary>Reads a canvas file and returns a complete HTML document.</summary>
    public static string RenderFile(string path)
    {
        var source = File.ReadAllText(path);
        var title = Path.GetFileNameWithoutExtension(path);
        if (title.EndsWith(".canvas", StringComparison.OrdinalIgnoreCase))
            title = title[..^".canvas".Length];
        return Render(source, title);
    }

    /// <summary>Renders canvas source to a complete HTML document.</summary>
    public static string Render(string canvasSource, string? title = null)
        => CanvasHtmlDocument.Wrap(title, RenderBody(canvasSource));

    /// <summary>Renders canvas source to the inner HTML placed in a document body.</summary>
    public static string RenderBody(string canvasSource)
    {
        var transpiled = new TsxTranspiler().Transpile(canvasSource);
        if (!EntryPattern.IsMatch(transpiled.EntryPoint))
            throw new CanvasRenderException("Unsafe canvas entry point '" + transpiled.EntryPoint + "'.");

        using var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(60));
            options.LimitRecursion(2048);
            options.MaxStatements(10_000_000);
        });

        try
        {
            engine.Execute(CanvasJsRuntime.Source);
            engine.Execute(transpiled.JavaScript);
            engine.Execute("var __canvasHtml = __render(" + transpiled.EntryPoint + "());");
        }
        catch (Exception ex) when (ex is not CanvasRenderException)
        {
            throw new CanvasRenderException(Describe(ex, transpiled.JavaScript), ex);
        }

        return engine.Evaluate("__canvasHtml")?.ToString() ?? "";
    }

    /// <summary>Builds one self-contained HTML document with a tab per canvas.</summary>
    public static string Combine(IReadOnlyList<CanvasPage> pages, string? title = null)
    {
        if (pages.Count == 0)
            throw new CanvasRenderException("A combined document needs at least one canvas.");

        return CanvasHtmlDocument.WrapCollection(title, pages);
    }

    private static readonly Regex EntryPattern = new(@"^[A-Za-z_$][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant);

    private static readonly Regex LinePattern = new(@"<anonymous>:(\d+):(\d+)", RegexOptions.CultureInvariant);

    private static string Describe(Exception exception, string javascript)
    {
        var match = LinePattern.Match(exception.Message);
        if (!match.Success)
            return exception.Message;

        var line = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var column = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        var rows = javascript.Split('\n');
        if (line <= 0 || line > rows.Length)
            return exception.Message;

        var text = rows[line - 1];
        var start = Math.Max(0, column - 80);
        var length = Math.Min(160, text.Length - start);
        var sample = length > 0 ? text.Substring(start, length) : text;
        return exception.Message + " near: " + sample.Trim();
    }
}
