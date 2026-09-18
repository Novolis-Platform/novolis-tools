using System.Text.Json;
using System.Xml.Linq;

namespace Novolis.Tools.Manifest;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static int Main(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(command))
        {
            PrintUsage();
            return 2;
        }

        var repo = GetOption(args, "--repo") ?? FindRepoRoot();
        var manifestPath = GetOption(args, "--manifest")
            ?? Path.Combine(repo, "build", "tools.json");

        try
        {
            var manifest = Load(manifestPath);
            return command switch
            {
                "validate" => Validate(manifest, repo),
                "list" => List(manifest),
                "generate-solutions" => GenerateSolution(manifest, repo),
                "ci-matrix" => EmitCiMatrix(
                    manifest,
                    repo,
                    GetChangedFiles(args)),
                _ => Unknown(command),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static ToolManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Tools manifest not found: {path}");

        return JsonSerializer.Deserialize<ToolManifest>(
                   File.ReadAllText(path),
                   JsonOptions)
               ?? throw new InvalidOperationException("Failed to parse tools manifest.");
    }

    private static int Validate(ToolManifest manifest, string repo)
    {
        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in manifest.Tools)
        {
            if (!keys.Add(tool.Key))
                errors.Add($"Duplicate tool key: {tool.Key}");
            if (!packageIds.Add(tool.PackageId))
                errors.Add($"Duplicate tool package id: {tool.PackageId}");
            if (!commands.Add(tool.Command))
                errors.Add($"Duplicate tool command: {tool.Command}");

            var projectPath = Resolve(repo, tool.Project);
            if (!File.Exists(projectPath))
            {
                errors.Add($"{tool.Key}: project does not exist: {tool.Project}");
                continue;
            }

            var xml = XDocument.Load(projectPath);
            var properties = xml.Descendants("PropertyGroup")
                .Elements()
                .ToDictionary(
                    e => e.Name.LocalName,
                    e => e.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase);

            if (!string.Equals(properties.GetValueOrDefault("OutputType"), "Exe", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{tool.Key}: host must set OutputType=Exe");
            if (!string.Equals(properties.GetValueOrDefault("PackAsTool"), "true", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{tool.Key}: host must set PackAsTool=true");
            if (!string.Equals(properties.GetValueOrDefault("IsPackable"), "true", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{tool.Key}: host must set IsPackable=true");
            if (!string.Equals(properties.GetValueOrDefault("ToolCommandName"), tool.Command, StringComparison.OrdinalIgnoreCase))
                errors.Add($"{tool.Key}: ToolCommandName does not match '{tool.Command}'");
            if (!string.Equals(properties.GetValueOrDefault("PackageId"), tool.PackageId, StringComparison.OrdinalIgnoreCase))
                errors.Add($"{tool.Key}: PackageId does not match '{tool.PackageId}'");

            foreach (var test in tool.Tests)
            {
                if (!File.Exists(Resolve(repo, test)))
                    errors.Add($"{tool.Key}: test project does not exist: {test}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                Console.Error.WriteLine(error);
            return 1;
        }

        Console.WriteLine($"tools manifest valid ({manifest.Tools.Count} tools)");
        return 0;
    }

    private static int List(ToolManifest manifest)
    {
        foreach (var tool in manifest.Tools.OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"{tool.Key}\t{tool.Command}\t{tool.Project}");
        return 0;
    }

    private static int GenerateSolution(ToolManifest manifest, string repo)
    {
        var output = Path.Combine(repo, "build", "Tools.Hosts.slnx");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        var lines = new List<string>
        {
            "<Solution>",
            "  <Folder Name=\"/src/\">",
        };

        foreach (var tool in manifest.Tools.OrderBy(t => t.Project, StringComparer.OrdinalIgnoreCase))
            lines.Add($"    <Project Path=\"{tool.Project.Replace('\\', '/')}\" />");

        lines.Add("  </Folder>");
        lines.Add("</Solution>");
        File.WriteAllText(output, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        Console.WriteLine(output);
        return 0;
    }

    private static int EmitCiMatrix(
        ToolManifest manifest,
        string repo,
        IReadOnlyList<string> changedFiles)
    {
        var selected = changedFiles.Count == 0
            ? manifest.Tools
            : manifest.Tools.Where(tool => tool.ChangedPathGlobs.Any(glob =>
                changedFiles.Any(file => Matches(file, glob)))).ToList();

        var selectedList = selected.ToList();
        var matrix = new
        {
            skip_build = selectedList.Count == 0,
            include = selectedList.Select(tool => new
            {
                key = tool.Key,
                project = Normalize(tool.Project),
                tests = tool.Tests.Select(Normalize).ToArray(),
            }).ToArray(),
        };

        Console.WriteLine(JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = false }));
        return 0;
    }

    private static IReadOnlyList<string> GetChangedFiles(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase)))
            return [];

        var files = GetRepeatedOption(args, "--changed-file").ToList();
        var changedFileList = GetOption(args, "--changed-files");
        if (!string.IsNullOrWhiteSpace(changedFileList) && File.Exists(changedFileList))
            files.AddRange(File.ReadLines(changedFileList));
        return files;
    }

    private static bool Matches(string file, string glob)
    {
        var normalizedFile = Normalize(file);
        var normalizedGlob = Normalize(glob)
            .Replace(".", "\\.")
            .Replace("**", "\u0000")
            .Replace("*", "[^/]*")
            .Replace("\u0000", ".*");
        return System.Text.RegularExpressions.Regex.IsMatch(
            normalizedFile,
            $"^{normalizedGlob.TrimEnd('/')}(?:/.*)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string Resolve(string repo, string relative) =>
        Path.GetFullPath(Path.Combine(repo, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Normalize(string value) => value.Replace('\\', '/').TrimStart('/');

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static IReadOnlyList<string> GetRepeatedOption(string[] args, string name)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                values.Add(args[i + 1]);
        }

        return values;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "build", "tools.json")))
                return current.FullName;
            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage() =>
        Console.Error.WriteLine(
            "Usage: ToolsManifest <validate|list|generate-solutions|ci-matrix> [--repo PATH] [--changed-file PATH]");

    private sealed class ToolManifest
    {
        public List<ToolEntry> Tools { get; init; } = [];
    }

    private sealed class ToolEntry
    {
        public string Key { get; init; } = string.Empty;
        public string Project { get; init; } = string.Empty;
        public string PackageId { get; init; } = string.Empty;
        public string Command { get; init; } = string.Empty;
        public List<string> Tests { get; init; } = [];
        public List<string> ChangedPathGlobs { get; init; } = [];
    }
}
