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
        sb.AppendLine($"""<div class="section-heading"><h2>{OrgStatusMarks.Chip("fail", "Failed")}</h2></div>""");
        if (status.Failures.Count == 0)
        {
            sb.AppendLine("<p>No failed or cancelled merge or release runs in the latest completed workflow for each repository.</p>");
        }
        else
        {
            sb.AppendLine($"""<div class="status-scroll"><table class="status-table"><thead><tr><th>When</th><th>Repository</th><th>{OrgStatusMarks.Chip("merge", "Workflow")}</th><th>{OrgStatusMarks.Chip("fail", "Run")}</th><th>Error</th></tr></thead><tbody>""");
            foreach (var row in status.Failures)
            {
                var detail = string.IsNullOrWhiteSpace(row.Error) ? row.Title : row.Error;
                sb.Append("<tr><td class=\"status-mono\">").Append(Encode(row.When)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td>").Append(WorkflowMark(row.Workflow)).Append("</td>");
                sb.Append("<td>").Append(ConclusionMark(row.Conclusion, row.Url)).Append("</td>");
                sb.Append("<td>").Append(Encode(detail)).Append("</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.AppendLine("</section>");
        sb.AppendLine("""<section id="shipped" class="section status-band">""");
        sb.AppendLine($"""<div class="section-heading"><h2>{OrgStatusMarks.Chip("ship", "Shipped")}</h2></div>""");
        if (status.Releases.Count == 0)
        {
            sb.AppendLine("<p>No GitHub Releases published.</p>");
        }
        else
        {
            sb.AppendLine($"""<div class="status-scroll"><table class="status-table"><thead><tr><th>Published</th><th>Repository</th><th>Tag</th><th>{OrgStatusMarks.Chip("ship", "Channel")}</th></tr></thead><tbody>""");
            foreach (var row in status.Releases)
            {
                sb.Append("<tr><td class=\"status-mono\">").Append(Encode(row.Published)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td><a class=\"status-mono\" href=\"").Append(Encode(row.Url)).Append("\">").Append(Encode(row.Tag)).Append("</a></td>");
                sb.Append("<td class=\"status-channel\">").Append(ChannelMark(row.Channel)).Append("</td></tr>");
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
            sb.Append(VersionChip("package", "GitHub Packages", repo.GprVersion));
        }

        if (!string.IsNullOrWhiteSpace(repo.NugetVersion))
        {
            sb.Append(VersionChip("nuget", "nuget.org", repo.NugetVersion));
        }

        if (!string.IsNullOrWhiteSpace(repo.ReleaseTag))
        {
            sb.Append(VersionChip("ship", "Release", repo.ReleaseTag));
        }

        if (repo.MergeConclusion is "failure" or "cancelled")
        {
            sb.Append(ConclusionMark(repo.MergeConclusion, repo.MergeUrl));
        }

        if (repo.ReleaseConclusion is "failure" or "cancelled")
        {
            sb.Append(ConclusionMark(repo.ReleaseConclusion, repo.ReleaseRunUrl));
        }

        return sb.ToString();
    }

    private static string VersionChip(string kind, string label, string version) =>
        $"""<span class="status-chip">{OrgStatusMarks.Chip(kind, label)}<span class="status-mono">{Encode(version)}</span></span>""";

    private static string WorkflowMark(string workflow) =>
        workflow.Contains("release", StringComparison.OrdinalIgnoreCase)
            ? OrgStatusMarks.Chip("ship", "release.yml")
            : OrgStatusMarks.Chip("merge", "merge.yml");

    private static string ConclusionMark(string conclusion, string url)
    {
        var kind = conclusion switch
        {
            "success" => "ok",
            "cancelled" => "cancel",
            "failure" => "fail",
            _ => "",
        };
        if (kind.Length == 0)
        {
            return "—";
        }

        var chip = OrgStatusMarks.Chip(kind, conclusion);
        return string.IsNullOrWhiteSpace(url)
            ? chip
            : $"""<a class="mark-{kind}" href="{Encode(url)}">{chip}</a>""";
    }

    private static string ChannelMark(string channel)
    {
        if (channel.StartsWith("nuget.org ", StringComparison.Ordinal))
        {
            return VersionChip("nuget", "nuget.org", channel["nuget.org ".Length..]);
        }

        if (channel.EndsWith("release asset", StringComparison.Ordinal) ||
            channel.EndsWith("release assets", StringComparison.Ordinal))
        {
            var count = channel.Split(' ')[0];
            return VersionChip("ship", "Release assets", count);
        }

        if (channel == "GitHub Release")
        {
            return OrgStatusMarks.Chip("ship", "GitHub Release");
        }

        return Encode(channel);
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
