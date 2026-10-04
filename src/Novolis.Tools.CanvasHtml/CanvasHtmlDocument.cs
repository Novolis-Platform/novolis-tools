using System.Net;
using System.Text;

namespace Novolis.Tools.CanvasHtml;

/// <summary>Wraps a rendered canvas body in one HTML document.</summary>
internal static class CanvasHtmlDocument
{
    internal static string Wrap(string? title, string body)
        => Document(title, body, extraCss: null);

    internal static string WrapCollection(string? title, IReadOnlyList<CanvasPage> pages)
    {
        var builder = new StringBuilder();
        var rules = new StringBuilder();
        builder.AppendLine("<div class=\"cv-collection\">");
        for (var i = 0; i < pages.Count; i++)
        {
            builder.Append("<input class=\"cv-tab-input\" type=\"radio\" name=\"cv-tab\" id=\"cv-tab-")
                .Append(i)
                .Append('"');
            if (i == 0)
                builder.Append(" checked");
            builder.AppendLine(">");
            rules.Append(".cv-collection:has(#cv-tab-").Append(i).Append(":checked) #cv-panel-").Append(i)
                .AppendLine(" { display: block; }");
            rules.Append(".cv-collection:has(#cv-tab-").Append(i).Append(":checked) label[for=\"cv-tab-").Append(i)
                .AppendLine("\"] { color: var(--fg); border-bottom-color: var(--accent); }");
        }

        builder.AppendLine("<div class=\"cv-tabbar\" role=\"tablist\">");
        for (var i = 0; i < pages.Count; i++)
        {
            builder.Append("<label for=\"cv-tab-").Append(i).Append("\" role=\"tab\">")
                .Append(WebUtility.HtmlEncode(pages[i].Title))
                .AppendLine("</label>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"cv-panels\">");
        for (var i = 0; i < pages.Count; i++)
        {
            builder.Append("<section class=\"cv-panel\" id=\"cv-panel-").Append(i).AppendLine("\" role=\"tabpanel\">");
            builder.AppendLine(pages[i].Body);
            builder.AppendLine("</section>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</div>");
        return Document(title, builder.ToString(), CollectionCss + rules);
    }

    private static string Document(string? title, string body, string? extraCss)
    {
        var safeTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "Canvas" : title);
        var builder = new StringBuilder(body.Length + 4096);
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>").Append(safeTitle).AppendLine("</title>");
        builder.AppendLine("<style>");
        builder.AppendLine(Css);
        if (!string.IsNullOrEmpty(extraCss))
            builder.AppendLine(extraCss);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine(body);
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private const string Css = """
        :root {
          color-scheme: dark;
          --bg: #181818;
          --fg: #e8e8e8;
          --muted: #b5b5b5;
          --dim: #8e8e8e;
          --faint: #6a6a6a;
          --stroke: rgba(255, 255, 255, 0.14);
          --fill: rgba(255, 255, 255, 0.05);
          --accent: #70b0d8;
          --ok: #1f8a65;
          --warn: #c4a035;
          --danger: #c04848;
          --info: #2e79b5;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          background: var(--bg);
          color: var(--fg);
          font: 14px/20px "Segoe UI", sans-serif;
          padding: 24px;
        }
        h1, h2, h3, p { margin: 0; }
        h1 { font-size: 24px; line-height: 30px; font-weight: 590; }
        h2 { font-size: 18px; line-height: 24px; font-weight: 590; margin-top: 8px; }
        h3 { font-size: 16px; line-height: 22px; font-weight: 590; }
        a { color: var(--accent); }
        code, kbd, .cv-pre {
          font-family: Consolas, ui-monospace, monospace;
          font-size: 0.92em;
        }
        code { background: var(--fill); padding: 0 4px; }
        svg { max-width: 100%; height: auto; display: block; }
        .cv-text { display: block; }
        .cv-size-small { font-size: 12px; line-height: 16px; }
        .cv-tone-primary { color: var(--fg); }
        .cv-tone-secondary { color: var(--muted); }
        .cv-tone-tertiary { color: var(--dim); }
        .cv-tone-quaternary { color: var(--faint); }
        .cv-tone-success { color: var(--ok); }
        .cv-tone-danger { color: var(--danger); }
        .cv-tone-warning { color: var(--warn); }
        .cv-tone-info { color: var(--info); }
        .cv-w-medium { font-weight: 500; }
        .cv-w-semibold { font-weight: 600; }
        .cv-w-bold { font-weight: 700; }
        .cv-italic { font-style: italic; }
        .cv-divider { border: 0; border-top: 1px solid var(--stroke); margin: 8px 0; }
        .cv-spacer { flex: 1; }
        .cv-card {
          border: 1px solid var(--stroke);
          background: #1e1e1e;
          border-radius: 8px;
          overflow: hidden;
        }
        .cv-card-plain { border: 0; background: transparent; }
        .cv-card-h {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 12px;
          min-height: 28px;
          padding: 6px 12px;
          font-size: 12px;
          line-height: 16px;
          border-bottom: 1px solid var(--stroke);
        }
        .cv-card-title { font-weight: 600; }
        .cv-card-trail { color: var(--dim); }
        .cv-card-b { padding: 12px; }
        .cv-pill {
          display: inline-flex;
          align-items: center;
          gap: 6px;
          border: 1px solid var(--stroke);
          border-radius: 999px;
          padding: 2px 8px;
          font-size: 12px;
          line-height: 16px;
          color: var(--muted);
        }
        .cv-pill-sm { font-size: 11px; padding: 0 6px; }
        .cv-pill-on { background: var(--fill); color: var(--fg); }
        .cv-stat { min-width: 96px; }
        .cv-stat-v { font-size: 22px; line-height: 28px; font-weight: 600; }
        .cv-stat-l { color: var(--dim); font-size: 12px; line-height: 16px; }
        .cv-callout {
          border: 1px solid var(--stroke);
          border-left-width: 3px;
          border-radius: 6px;
          padding: 10px 12px;
        }
        .cv-callout.cv-tone-info { border-left-color: var(--info); }
        .cv-callout.cv-tone-success { border-left-color: var(--ok); }
        .cv-callout.cv-tone-warning { border-left-color: var(--warn); }
        .cv-callout.cv-tone-danger { border-left-color: var(--danger); }
        .cv-callout.cv-tone-neutral { border-left-color: var(--dim); }
        .cv-callout-title { font-weight: 600; margin-bottom: 4px; }
        .cv-table-frame { overflow-x: auto; border: 1px solid var(--stroke); border-radius: 8px; }
        .cv-table { width: 100%; border-collapse: collapse; font-size: 13px; line-height: 18px; }
        .cv-table th, .cv-table td { padding: 6px 10px; border-bottom: 1px solid var(--stroke); vertical-align: top; text-align: left; }
        .cv-table th { color: var(--dim); font-weight: 600; font-size: 12px; }
        .cv-striped tbody tr:nth-child(even) { background: var(--fill); }
        .cv-row-success td:first-child { border-left: 3px solid var(--ok); }
        .cv-row-danger td:first-child { border-left: 3px solid var(--danger); }
        .cv-row-warning td:first-child { border-left: 3px solid var(--warn); }
        .cv-row-info td:first-child { border-left: 3px solid var(--info); }
        .cv-usage-labels, .cv-usage-legend { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; color: var(--dim); font-size: 12px; }
        .cv-usage-bar { display: flex; height: 10px; border-radius: 999px; overflow: hidden; background: var(--fill); margin: 6px 0; }
        .cv-usage-rest { background: rgba(255, 255, 255, 0.06); }
        .cv-usage-legend { gap: 10px; }
        .cv-usage-legend i, .cv-swatch { display: inline-block; width: 8px; height: 8px; border-radius: 2px; }
        .cv-todo { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
        .cv-todo li { display: flex; gap: 10px; align-items: flex-start; }
        .cv-todo-status { flex: 0 0 auto; color: var(--dim); font-size: 12px; min-width: 88px; }
        .cv-todo-completed .cv-todo-status { color: var(--ok); }
        .cv-todo-in_progress .cv-todo-status { color: var(--warn); }
        .cv-todo-cancelled .cv-todo-status { color: var(--faint); }
        .cv-fold { border-top: 1px solid var(--stroke); padding: 8px 0; }
        .cv-fold summary { cursor: pointer; font-weight: 600; }
        .cv-fold-body { padding: 8px 0 0 16px; }
        .cv-button {
          border: 1px solid var(--stroke);
          background: transparent;
          color: var(--fg);
          border-radius: 6px;
          padding: 2px 10px;
          font: inherit;
        }
        .cv-button-primary { background: var(--info); border-color: var(--info); }
        .cv-pre { white-space: pre-wrap; margin: 0; padding: 12px; }
        """;

    private const string CollectionCss = """
        body:has(.cv-collection) { padding-top: 0; }
        .cv-tab-input {
          position: absolute;
          width: 1px;
          height: 1px;
          overflow: hidden;
          clip: rect(0 0 0 0);
        }
        .cv-tabbar {
          position: sticky;
          top: 0;
          z-index: 2;
          display: flex;
          gap: 4px;
          overflow-x: auto;
          background: var(--bg);
          border-bottom: 1px solid var(--stroke);
          padding: 8px 24px 0;
        }
        .cv-tabbar label {
          flex: 0 0 auto;
          padding: 8px 10px 6px;
          color: var(--dim);
          border-bottom: 2px solid transparent;
          cursor: pointer;
          font-size: 13px;
          line-height: 16px;
        }
        .cv-panels { padding: 24px; }
        .cv-panel { display: none; }
        """;
}
