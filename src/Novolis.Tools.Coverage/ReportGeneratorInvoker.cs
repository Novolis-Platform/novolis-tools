using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>Locate / install and invoke ReportGenerator.</summary>
[ExcludeFromCodeCoverage] // Process host (install/PATH/reportgenerator invoke); see coverage-report.md
public static class ReportGeneratorInvoker
{
    /// <summary>Ensure <c>reportgenerator</c> is on PATH (install global tool if needed).</summary>
    public static void EnsureInstalled(TextWriter? log = null)
    {
        if (FindExecutable() is not null)
            return;

        log?.WriteLine("Installing dotnet-reportgenerator-globaltool...");
        var install = Run("dotnet", ["tool", "install", "-g", "dotnet-reportgenerator-globaltool"], log);
        if (install != 0)
        {
            // Already installed but not on PATH is fine; try update path.
            _ = Run("dotnet", ["tool", "update", "-g", "dotnet-reportgenerator-globaltool"], log);
        }

        var tools = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet", "tools");
        if (Directory.Exists(tools))
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!path.Contains(tools, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATH", tools + Path.PathSeparator + path);
        }

        if (FindExecutable() is null)
            throw new InvalidOperationException(
                "reportgenerator not found. Install: dotnet tool install -g dotnet-reportgenerator-globaltool");
    }

    /// <summary>
    /// Merge Cobertura into an advanced HTML report: class drill-down, risk hotspots (CRAP/CC),
    /// history chart, and badges. Pass <paramref name="historyDir"/> across runs for trends.
    /// </summary>
    public static int Generate(
        IReadOnlyList<string> coberturaFiles,
        string targetDir,
        string title,
        string reportTypes = "Html;HtmlSummary;HtmlChart;Badges;TextSummary;MarkdownSummaryGithub;Cobertura",
        string? assemblyFilters = null,
        string? historyDir = null,
        TextWriter? log = null)
    {
        EnsureInstalled(log);
        Directory.CreateDirectory(targetDir);
        var reports = string.Join(';', coberturaFiles);
        var exe = FindExecutable() ?? "reportgenerator";
        var args = new List<string>
        {
            $"-reports:{reports}",
            $"-targetdir:{targetDir}",
            $"-reporttypes:{reportTypes}",
            $"-title:{title}",
            "-filefilters:-*MessagePack.SourceGenerator*;-*.g.cs",
            "-classfilters:-*.Tests*;-*Test;-*Tests;-MessagePack.*;-Frank.*",
            $"-assemblyfilters:{(string.IsNullOrWhiteSpace(assemblyFilters) ? "-Novolis.Analyzers.Licensing" : assemblyFilters)}",
        };

        if (!string.IsNullOrWhiteSpace(historyDir))
        {
            Directory.CreateDirectory(historyDir);
            args.Add($"-historydir:{historyDir}");
        }

        return Run(exe, args, log);
    }

    private static string? FindExecutable()
    {
        var name = OperatingSystem.IsWindows() ? "reportgenerator.exe" : "reportgenerator";
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), name);
            if (File.Exists(candidate))
                return candidate;
        }

        var tools = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet", "tools", name);
        return File.Exists(tools) ? tools : null;
    }

    private static int Run(string fileName, IReadOnlyList<string> args, TextWriter? log)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (!string.IsNullOrWhiteSpace(stdout))
            log?.WriteLine(stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(stderr))
            log?.WriteLine(stderr.TrimEnd());
        return p.ExitCode;
    }
}
