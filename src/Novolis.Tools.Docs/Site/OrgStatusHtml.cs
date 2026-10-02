using System.Net;
using System.Text;
using Novolis.Tools.Docs.Org;

namespace Novolis.Tools.Docs.Site;

/// <summary>Renders release status bands for the portfolio docs home.</summary>
public static partial class OrgStatusHtml
{
    /// <summary>Failed runs, shipped releases. Empty when the snapshot has neither list to introduce.</summary>
    public static string Bands(OrgStatusSnapshot status)
    {
        var sb = new StringBuilder();
        sb.Append("<section id=\"failed\" class=\"section status-band\" data-org=\"").Append(Encode(status.Org)).AppendLine("\">");
        sb.AppendLine($"""<div class="section-heading"><h2 class="status-label mark-fail">{OrgStatusMarks.Svg("fail")}<span>Failed</span></h2></div>""");
        if (status.Failures.Count == 0)
        {
            sb.AppendLine("<p>No failed or cancelled merge runs.</p>");
        }
        else
        {
            sb.AppendLine("""<div class="status-scroll"><table class="status-table"><thead><tr><th>When</th><th>Repository</th><th>Workflow</th><th>Result</th><th>Error</th></tr></thead><tbody>""");
            foreach (var row in status.Failures)
            {
                var detail = string.IsNullOrWhiteSpace(row.Error) ? row.Title : row.Error;
                sb.Append("<tr data-repo=\"").Append(Encode(row.Repo)).Append("\" data-workflow=\"").Append(Encode(row.Workflow)).Append("\" data-run=\"").Append(Encode(row.Url)).Append("\"><td class=\"status-mono\">").Append(Encode(row.When)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td>").Append(WorkflowName(row.Workflow)).Append("</td>");
                sb.Append("<td>").Append(ConclusionMark(row.Conclusion, row.Url)).Append("</td>");
                sb.Append("<td>").Append(Encode(detail)).Append("</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.AppendLine("</section>");
        sb.AppendLine("""<section id="shipped" class="section status-band">""");
        sb.AppendLine($"""<div class="section-heading"><h2 class="status-label mark-ship">{OrgStatusMarks.Svg("ship")}<span>Shipped</span></h2></div>""");
        if (status.Releases.Count == 0)
        {
            sb.AppendLine("<p>No app release has published installers or other assets.</p>");
        }
        else
        {
            sb.AppendLine("""<div class="status-scroll"><table class="status-table"><thead><tr><th>Published</th><th>Repository</th><th>Tag</th><th>Channel</th></tr></thead><tbody>""");
            foreach (var row in status.Releases)
            {
                sb.Append("<tr><td class=\"status-mono\">").Append(Encode(row.Published)).Append("</td>");
                sb.Append("<td><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(row.Repo)).Append("\">").Append(Encode(row.Repo)).Append("</a></td>");
                sb.Append("<td><a class=\"status-mono\" href=\"").Append(Encode(row.Url)).Append("\">").Append(Encode(row.Tag)).Append("</a></td>");
                sb.Append("<td class=\"status-channel\">").Append(ChannelMark(row.Channel)).Append("</td></tr>");
            }

            sb.AppendLine("</tbody></table></div>");
        }

        sb.AppendLine("</section>");
        sb.Append(Downloads(status));
        sb.Append("<p class=\"status-stamp\" id=\"status-stamp\">Snapshot ").Append(Encode(status.GeneratedAt)).AppendLine("</p>");
        return sb.ToString();
    }

    /// <summary>Mono facts for a library card. Empty when the repository is absent from the snapshot.</summary>
    public static string CardFacts(OrgStatusRepo? repo, string? org = null)
    {
        if (repo is null)
        {
            return "";
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(repo.GprVersion))
        {
            var packagesUrl = string.IsNullOrWhiteSpace(org) || repo.PackageCount <= 0
                ? null
                : $"https://github.com/orgs/{org}/packages?repo_name={Uri.EscapeDataString(repo.Name)}";
            sb.Append(VersionChip("package", "Packages", repo.GprVersion, packagesUrl));
        }

        if (repo.PackageCount > 0)
        {
            var nuget = string.IsNullOrWhiteSpace(repo.NugetVersion) ? "missing" : repo.NugetVersion;
            sb.Append(VersionChip("nuget", "nuget.org", nuget));
        }

        if (!string.IsNullOrWhiteSpace(repo.ReleaseTag))
        {
            var releaseUrl = string.IsNullOrWhiteSpace(repo.ReleaseUrl) ? null : repo.ReleaseUrl;
            sb.Append(VersionChip("ship", "Release", repo.ReleaseTag, releaseUrl));
        }

        if (repo.MergeConclusion is "failure" or "cancelled")
        {
            sb.Append(ConclusionMark(repo.MergeConclusion, repo.MergeUrl, "Merge " + Word(repo.MergeConclusion)));
        }

        if (OrgShipPath.PublishesInstallers(repo.Name)
            && repo.ReleaseConclusion is "failure" or "cancelled")
        {
            sb.Append(ConclusionMark(repo.ReleaseConclusion, repo.ReleaseRunUrl, "Release " + Word(repo.ReleaseConclusion)));
        }

        return sb.ToString();
    }

    private static string VersionChip(string kind, string label, string version, string? href = null)
    {
        var body = $"""{OrgStatusMarks.Svg(kind)}<span>{Encode(label)}</span><span class="status-mono">{Encode(version)}</span>""";
        var css = $"status-chip mark-{Encode(kind)}";
        return string.IsNullOrWhiteSpace(href)
            ? $"""<span class="{css}">{body}</span>"""
            : $"""<a class="{css}" href="{Encode(href)}">{body}</a>""";
    }

    private static string WorkflowName(string workflow) =>
        workflow.Contains("release", StringComparison.OrdinalIgnoreCase) ? "release" : "merge";

    private static string Word(string conclusion) => conclusion switch
    {
        "success" => "passed",
        "cancelled" => "cancelled",
        "failure" => "failed",
        _ => conclusion,
    };

    private static string ConclusionMark(string conclusion, string url, string? label = null)
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

        var text = Encode(label ?? Word(conclusion));
        var body = $"""{OrgStatusMarks.Svg(kind)}<span>{text}</span>""";
        return string.IsNullOrWhiteSpace(url)
            ? $"""<span class="status-label mark-{kind}">{body}</span>"""
            : $"""<a class="status-label mark-{kind}" href="{Encode(url)}">{body}</a>""";
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
            var label = count + " assets";
            return $"""<a class="status-label mark-ship" href="#downloads">{OrgStatusMarks.Svg("ship")}<span>{Encode(label)}</span></a>""";
        }

        if (channel == "GitHub Release")
        {
            return OrgStatusMarks.Label("ship", "GitHub Release");
        }

        return Encode(channel);
    }

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
