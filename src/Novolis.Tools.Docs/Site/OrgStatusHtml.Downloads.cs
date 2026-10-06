using System.Text;
using Novolis.Tools.Docs.Org;

namespace Novolis.Tools.Docs.Site;

public static partial class OrgStatusHtml
{
    /// <summary>Latest installer and package downloads for shipped app releases.</summary>
    public static string Downloads(OrgStatusSnapshot status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<section id="downloads" class="section status-band">""");
        sb.AppendLine($"""<div class="section-heading"><h2 class="status-label mark-ship">{OrgStatusMarks.Svg("ship")}<span>Latest app downloads</span></h2></div>""");
        var groups = OrgDownloadCatalog.From(status.Releases);
        var anyFiles = groups.Any(static group => group.Apps.Count > 0 || group.Checksums.Count > 0);
        if (!anyFiles)
        {
            if (status.Releases.Count == 0)
            {
                sb.AppendLine("<p>No app release has published installers or other assets.</p>");
            }
            else
            {
                foreach (var release in status.Releases)
                {
                    sb.Append("<p><a href=\"https://github.com/").Append(Encode(status.Org)).Append('/').Append(Encode(release.Repo)).Append("\">").Append(Encode(release.Repo)).Append("</a> ");
                    AppendAnchor(sb, release.Url, release.Tag);
                    sb.AppendLine("</p>");
                }
            }

            sb.AppendLine("</section>");
            return sb.ToString();
        }

        foreach (var group in groups)
        {
            sb.Append("<p class=\"download-source\">Latest files from <a href=\"https://github.com/")
                .Append(Encode(status.Org)).Append('/').Append(Encode(group.Repo)).Append("\">")
                .Append(Encode(group.Repo)).Append("</a> <a href=\"").Append(Encode(group.Url)).Append("\">")
                .Append(Encode(group.Tag)).Append("</a>");
            if (!string.IsNullOrWhiteSpace(group.Published))
            {
                sb.Append(", published ").Append(Encode(group.Published));
            }

            sb.AppendLine(".</p>");
            if (group.Apps.Count == 0)
            {
                continue;
            }

            sb.AppendLine("""<div class="status-scroll"><table class="status-table download-table"><thead><tr><th>App</th><th>Windows</th><th>Android</th><th>Linux</th></tr></thead><tbody>""");
            foreach (var app in group.Apps)
            {
                sb.Append("<tr><td>").Append(Encode(app.Name)).Append("</td>");
                sb.Append(AssetCell(app.Windows, "Windows"));
                sb.Append(AssetCell(app.Android, "Android"));
                sb.Append(AssetCell(app.Linux, "Linux"));
                sb.AppendLine("</tr>");
                if (app.Other.Count > 0)
                {
                    sb.Append("<tr><td>").Append(Encode(app.Name)).Append("</td><td colspan=\"3\">");
                    sb.Append(AssetLinks(app.Other, "Download"));
                    sb.AppendLine("</td></tr>");
                }
            }

            sb.AppendLine("</tbody></table></div>");
            if (group.Checksums.Count > 0)
            {
                sb.Append("<p class=\"download-checksums\">");
                for (var i = 0; i < group.Checksums.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(" · ");
                    }

                    var asset = group.Checksums[i];
                    AppendAnchor(sb, asset.Url, asset.Name);
                }

                sb.AppendLine("</p>");
            }
        }

        sb.AppendLine("</section>");
        return sb.ToString();
    }

    /// <summary>Quoted href so a raw-string closer cannot swallow the opening quote and leave it inside the URL.</summary>
    private static void AppendAnchor(StringBuilder sb, string url, string text, string? title = null)
    {
        sb.Append("<a href=\"").Append(Encode(url)).Append('"');
        if (!string.IsNullOrEmpty(title))
        {
            sb.Append(" title=\"").Append(Encode(title)).Append('"');
        }

        sb.Append('>').Append(Encode(text)).Append("</a>");
    }

    private static string AssetCell(IReadOnlyList<OrgStatusAsset> assets, string platform)
    {
        if (assets.Count == 0)
        {
            return "<td>—</td>";
        }

        return "<td>" + AssetLinks(assets, platform) + "</td>";
    }

    private static string AssetLinks(IReadOnlyList<OrgStatusAsset> assets, string platform)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < assets.Count; i++)
        {
            if (i > 0)
            {
                sb.Append("<br>");
            }

            var asset = assets[i];
            var size = OrgDownloadCatalog.FormatSize(asset.Size);
            var label = string.IsNullOrEmpty(size) ? platform : platform + " · " + size;
            AppendAnchor(sb, asset.Url, label, asset.Name);
        }

        return sb.ToString();
    }
}
