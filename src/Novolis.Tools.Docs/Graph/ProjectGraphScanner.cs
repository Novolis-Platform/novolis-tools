using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Docs.Graph;

/// <summary>Scans a directory tree of <c>.csproj</c> files into a <see cref="RelationshipGraph"/>.</summary>
public static partial class ProjectGraphScanner
{
    /// <summary>
    /// Walks <paramref name="root"/> for projects and records ProjectReference + PackageReference edges.
    /// </summary>
    /// <param name="root">Directory to scan.</param>
    /// <param name="includePackages">When true, include NuGet PackageReference edges.</param>
    /// <param name="includeNovolisPackagesOnly">When true with packages, keep only <c>Novolis.*</c> package edges.</param>
    public static RelationshipGraph Scan(
        string root,
        bool includePackages = true,
        bool includeNovolisPackagesOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var rootFull = Path.GetFullPath(root);
        if (!Directory.Exists(rootFull))
        {
            throw new DirectoryNotFoundException($"Root not found: {rootFull}");
        }

        var graph = new RelationshipGraph();
        var projects = Directory.EnumerateFiles(rootFull, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsIgnoredPath(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in projects)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            byPath[path] = name;
            graph.AddNode(name, name, GraphNodeKind.Project);
        }

        foreach (var path in projects)
        {
            var from = byPath[path];
            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch (Exception ex)
            {
                graph.AddNode(from, $"{from} (unreadable)", GraphNodeKind.Project);
                graph.AddEdge(from, $"parse_error_{from}", ex.GetType().Name, GraphNodeKind.Project, GraphNodeKind.External);
                continue;
            }

            var projectDir = Path.GetDirectoryName(path)!;
            foreach (var pref in doc.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
            {
                var include = pref.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include))
                {
                    continue;
                }

                var targetPath = Path.GetFullPath(Path.Combine(projectDir, include));
                var toName = byPath.TryGetValue(targetPath, out var known)
                    ? known
                    : Path.GetFileNameWithoutExtension(targetPath);
                graph.AddEdge(from, toName, "ProjectReference", GraphNodeKind.Project, GraphNodeKind.Project);
            }

            if (!includePackages)
            {
                continue;
            }

            foreach (var pref in doc.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
            {
                var packageId = pref.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    continue;
                }

                if (includeNovolisPackagesOnly &&
                    !packageId.StartsWith("Novolis.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Skip infra noise that every project gets.
                if (packageId.StartsWith("Microsoft.SourceLink.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                graph.AddNode(packageId, packageId, GraphNodeKind.Package);
                graph.AddEdge(from, packageId, "PackageReference", GraphNodeKind.Project, GraphNodeKind.Package);
            }
        }

        return graph;
    }

    private static bool IsIgnoredPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return IgnoredSegmentRegex().IsMatch(normalized);
    }

    [GeneratedRegex(@"/(bin|obj|artifacts|\.git|node_modules)/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IgnoredSegmentRegex();
}
