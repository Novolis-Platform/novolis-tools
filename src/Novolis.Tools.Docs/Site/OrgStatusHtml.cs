using System.Net;
using System.Text;
using Novolis.Tools.Docs.Org;

namespace Novolis.Tools.Docs.Site;

/// <summary>Renders release status bands for the portfolio docs home.</summary>
public static class OrgStatusHtml
{
    /// <summary>Failed runs, shipped releases. Empty when the snapshot has neither list to introduce.</summary>
    public static string Bands(OrgStatusSnapshot status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<section id="failed" class="section status-band">""");
        sb.AppendLine("""<div class="section-heading"><p class="eyebrow">Latest completed merge.yml and release.yml</p><h2>Failed</h2></div>""");
        if (status.Failures.Count == 0)
        {
            sb.AppendLine("<p>No failed or cancelled merge or release runs in the latest completed workflow for each repository.</p>");
        }
        else
        {
            sb.AppendLine("""<div class="status-scroll"><table class="status-table"><thead><tr><th>When</th><th>Repository</th><th>Workflow</th><th>Run</th><th>Error</th></tr></thead><tbody>""");
            foreach (var row in status.Failures)
            {
                var detail = string.IsNullOrWhiteSpace(row.Error) ? row.Title : row.Error;
                sb.Append("<tr><td class=\"status-mono\">").Append(Encode(row.When)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td class=\"status-mono\">").Append(Encode(row.Workflow)).Append("</td>");
                sb.Append("<td><a class=\"status-fail\" href=\"").Append(Encode(row.Url)).Append("\">").Append(Encode(row.Conclusion)).Append("</a></td>");
                sb.Append("<td>").Append(Encode(detail)).Append("</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.AppendLine("</section>");
        sb.AppendLine("""<section id="shipped" class="section status-band">""");
        sb.AppendLine("""<div class="section-heading"><p class="eyebrow">GitHub Releases, not GPR build numbers</p><h2>Shipped</h2></div>""");
        if (status.Releases.Count == 0)
        {
            sb.AppendLine("<p>No GitHub Releases published.</p>");
        }
        else
        {
            sb.AppendLine("""<div class="status-scroll"><table class="status-table"><thead><tr><th>Published</th><th>Repository</th><th>Tag</th><th>Channel</th></tr></thead><tbody>""");
            foreach (var row in status.Releases)
            {
                sb.Append("<tr><td class=\"status-mono\">").Append(Encode(row.Published)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td><a class=\"status-mono\" href=\"").Append(Encode(row.Url)).Append("\">").Append(Encode(row.Tag)).Append("</a></td>");
                sb.Append("<td>").Append(Encode(row.Channel)).Append("</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.Append("<p class=\"status-stamp\">Snapshot ").Append(Encode(status.GeneratedAt)).AppendLine("</p>");
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    /// <summary>Mono facts for a library card. Empty when the repository is absent from the snapshot.</summary>
    public static string CardFacts(OrgStatusRepo? repo)
    {
        if (repo is null)
        {
            return "";
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(repo.GprVersion))
        {
            sb.Append("<span class=\"status-mono\">GPR ").Append(Encode(repo.GprVersion)).Append("</span>");
        }

        if (!string.IsNullOrWhiteSpace(repo.NugetVersion))
        {
            sb.Append("<span class=\"status-mono\">nuget.org ").Append(Encode(repo.NugetVersion)).Append("</span>");
        }

        if (!string.IsNullOrWhiteSpace(repo.ReleaseTag))
        {
            sb.Append("<span class=\"status-mono\">release ").Append(Encode(repo.ReleaseTag)).Append("</span>");
        }

        if (repo.MergeConclusion is "failure" or "cancelled")
        {
            sb.Append("<span class=\"status-fail\">merge ").Append(Encode(repo.MergeConclusion)).Append("</span>");
        }

        if (repo.ReleaseConclusion is "failure" or "cancelled")
        {
            sb.Append("<span class=\"status-fail\">release ").Append(Encode(repo.ReleaseConclusion)).Append("</span>");
        }

        return sb.ToString();
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
