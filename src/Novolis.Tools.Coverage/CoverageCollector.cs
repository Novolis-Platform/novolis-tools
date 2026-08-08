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

    /// <summary>Merge Cobertura files into HTML (+ optional extra report types).</summary>
    public static int Generate(
        IReadOnlyList<string> coberturaFiles,
        string targetDir,
        string title,
        string reportTypes = "Html;HtmlSummary;TextSummary;MarkdownSummaryGithub;Cobertura",
        string? assemblyFilters = null,
        TextWriter? log = null)
    {
        EnsureInstalled(log);
        Directory.CreateDirectory(targetDir);
        var reports = string.Join(';', coberturaFiles);
        var exe = FindExecutable() ?? "reportgenerator";
        return Run(exe, [
            $"-reports:{reports}",
            $"-targetdir:{targetDir}",
            $"-reporttypes:{reportTypes}",
            $"-title:{title}",
            "-filefilters:-*MessagePack.SourceGenerator*;-*.g.cs",
            "-classfilters:-*.Tests*;-*Test;-*Tests;-MessagePack.*;-Frank.*",
            $"-assemblyfilters:{(string.IsNullOrWhiteSpace(assemblyFilters) ? "-Novolis.Analyzers.Licensing" : assemblyFilters)}",
        ], log);
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

/// <summary>Orchestrates parallel <c>dotnet test --coverage</c> and ReportGenerator merge.</summary>
[ExcludeFromCodeCoverage] // Process orchestration host (dotnet test / ReportGenerator); helpers remain scored
public sealed class CoverageCollector
{
    private static readonly Regex TotalRe = new(@"total:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SucceededRe = new(@"succeeded:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FailedRe = new(@"failed:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TextWriter _log;

    /// <summary>Create a collector writing progress to <paramref name="log"/>.</summary>
    public CoverageCollector(TextWriter? log = null)
    {
        _log = log ?? TextWriter.Null;
    }

    /// <summary>Run collection according to <paramref name="options"/>.</summary>
    public async Task<CoverageCollectResult> CollectAsync(
        CoverageCollectOptions options,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var root = Path.GetFullPath(options.Root);
        var outputDir = Path.GetFullPath(options.OutputDir);
        var excludeFile = string.IsNullOrWhiteSpace(options.ExcludeFile)
            ? CoverageWorkspace.DefaultExcludeFile(root)
            : Path.GetFullPath(options.ExcludeFile);
        var excludes = CoverageWorkspace.ReadExcludes(excludeFile, options.Exclude);
        var includes = CoverageWorkspace.ExpandNames(options.Include);
        var includeSet = includes.Count > 0
            ? new HashSet<string>(includes, StringComparer.OrdinalIgnoreCase)
            : null;

        var failBelow = options.FailBelow;
        if (options.PlatformSlnx && failBelow == 0)
            failBelow = 95;

        var throttle = options.ThrottleLimit > 0
            ? options.ThrottleLimit
            : Math.Max(1, Environment.ProcessorCount - 1);

        if (options.PlatformSlnx && options.RegenerateSlnx)
            await RegeneratePlatformSlnxAsync(root, cancellationToken).ConfigureAwait(false);

        string? platformSlnx = null;
        IReadOnlyList<CoverageRepo> repos;
        if (options.PlatformSlnx)
        {
            platformSlnx = CoverageWorkspace.ResolvePlatformSlnx(root, options.PlatformSlnxPath);
            repos = TestHostDiscovery.DiscoverFromPlatformSlnx(root, platformSlnx, excludes, includeSet);
        }
        else
        {
            repos = TestHostDiscovery.DiscoverRepos(root, excludes, includeSet);
        }

        _log.WriteLine($"Coverage root: {root}");
        _log.WriteLine($"Output:        {outputDir}");
        _log.WriteLine($"Throttle:      {throttle}");
        _log.WriteLine($"Mode:          {(options.PlatformSlnx ? "Platform.slnx ProjectRef" : "NuGet per-repo")}");
        if (platformSlnx is not null)
            _log.WriteLine($"Solution:      {platformSlnx}");
        _log.WriteLine($"ProjectRef:    {(options.PlatformSlnx ? "true" : "false")}");
        _log.WriteLine($"FailBelow:     {failBelow}");
        _log.WriteLine($"Repos:         {repos.Count}");
        _log.WriteLine();

        if (options.ListOnly)
        {
            foreach (var r in repos)
                _log.WriteLine($"{r.Name}\t{r.TestProjects.Count}\t{r.Solution ?? "(none)"}");

            return new CoverageCollectResult
            {
                OutputDir = outputDir,
                HtmlIndexPath = null,
                SummaryMarkdownPath = Path.Combine(outputDir, "SUMMARY.md"),
                DurationSeconds = (DateTime.UtcNow - started).TotalSeconds,
                Repos = [],
            };
        }

        Directory.CreateDirectory(outputDir);
        var rawDir = Path.Combine(outputDir, "raw");
        var reportDir = Path.Combine(outputDir, "report");
        var logsDir = Path.Combine(outputDir, "logs");
        Directory.CreateDirectory(rawDir);
        Directory.CreateDirectory(reportDir);
        Directory.CreateDirectory(logsDir);

        foreach (var old in Directory.EnumerateFiles(rawDir, "*.cobertura.xml", SearchOption.AllDirectories))
            File.Delete(old);

        var projectRef = options.PlatformSlnx ? "true" : "false";
        var results = new CoverageRepoResult[repos.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, repos.Count),
            new ParallelOptions { MaxDegreeOfParallelism = throttle, CancellationToken = cancellationToken },
            async (i, ct) =>
            {
                results[i] = await CollectRepoAsync(
                    repos[i],
                    options.Configuration,
                    options.SkipBuild,
                    projectRef,
                    Path.Combine(rawDir, repos[i].Name),
                    Path.Combine(logsDir, repos[i].Name + ".log"),
                    ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

        var repoResults = results.OrderBy(r => r.Repo, StringComparer.OrdinalIgnoreCase).ToList();
        var allCobertura = new List<string>();
        foreach (var r in repoResults)
        {
            if (r.Status != "ok" || r.CoberturaFiles.Count == 0)
                continue;
            allCobertura.AddRange(r.CoberturaFiles);

            var repoReport = Path.Combine(reportDir, r.Repo);
            Directory.CreateDirectory(repoReport);
            if (r.CoberturaFiles.Count > 0)
            {
                ReportGeneratorInvoker.Generate(
                    r.CoberturaFiles,
                    repoReport,
                    r.Repo,
                    reportTypes: "Cobertura;TextSummary",
                    assemblyFilters: CoverageWorkspace.RepoAssemblyFilter(r.Repo),
                    log: TextWriter.Null);
            }
        }

        // Fill per-repo percents from merged or single files
        for (var i = 0; i < repoResults.Count; i++)
        {
            var r = repoResults[i];
            if (r.Status != "ok" || r.CoberturaFiles.Count == 0)
                continue;
            var merged = Path.Combine(reportDir, r.Repo, "Cobertura.xml");
            var path = File.Exists(merged) ? merged : r.CoberturaFiles[0];
            try
            {
                var sum = CoberturaSummaryParser.Parse(path);
                repoResults[i] = new CoverageRepoResult
                {
                    Repo = r.Repo,
                    Status = r.Status,
                    Error = r.Error,
                    Seconds = r.Seconds,
                    CoberturaFiles = r.CoberturaFiles,
                    TestsTotal = r.TestsTotal,
                    TestsPassed = r.TestsPassed,
                    TestsFailed = r.TestsFailed,
                    LinePercent = sum.LinePercent,
                    BranchPercent = sum.BranchPercent,
                    LinesCovered = sum.LinesCovered,
                    LinesValid = sum.LinesValid,
                };
            }
            catch
            {
                // keep without percents
            }
        }

        double? aggLine = null;
        double? aggBranch = null;
        string? htmlIndex = null;
        if (allCobertura.Count > 0)
        {
            _log.WriteLine();
            _log.WriteLine($"Merging {allCobertura.Count} cobertura file(s) with ReportGenerator...");
            var exit = ReportGeneratorInvoker.Generate(
                allCobertura,
                reportDir,
                "Novolis coverage",
                log: _log);
            if (exit != 0)
                throw new InvalidOperationException($"reportgenerator failed (exit {exit})");

            var aggCob = Path.Combine(reportDir, "Cobertura.xml");
            if (File.Exists(aggCob))
            {
                var agg = CoberturaSummaryParser.Parse(aggCob);
                aggLine = agg.LinePercent;
                aggBranch = agg.BranchPercent;
            }

            htmlIndex = Path.Combine(reportDir, "index.html");
            if (!File.Exists(htmlIndex))
                htmlIndex = null;
        }

        if (options.FlattenHtml && htmlIndex is not null)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(reportDir))
            {
                var name = Path.GetFileName(entry);
                if (name is "raw" or "logs")
                    continue;
                // Keep per-repo subdirs under report; only promote top-level report assets
                if (Directory.Exists(entry) && name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dest = Path.Combine(outputDir, name);
                if (Directory.Exists(dest))
                    Directory.Delete(dest, recursive: true);
                if (File.Exists(dest))
                    File.Delete(dest);
                if (Directory.Exists(entry))
                    Directory.Move(entry, dest);
                else
                    File.Move(entry, dest, overwrite: true);
            }

            var flat = Path.Combine(outputDir, "index.html");
            if (File.Exists(flat))
                htmlIndex = flat;
        }

        var mdPath = Path.Combine(outputDir, "SUMMARY.md");
        WriteSummaryMarkdown(
            mdPath,
            root,
            platformSlnx,
            options.PlatformSlnx,
            throttle,
            failBelow,
            (DateTime.UtcNow - started).TotalSeconds,
            aggLine,
            aggBranch,
            repoResults);

        var jsonPath = Path.Combine(outputDir, "summary.json");
        await File.WriteAllTextAsync(jsonPath, BuildSummaryJson(
            options.PlatformSlnx,
            platformSlnx,
            failBelow,
            (DateTime.UtcNow - started).TotalSeconds,
            throttle,
            aggLine,
            aggBranch,
            repoResults), cancellationToken).ConfigureAwait(false);

        _log.WriteLine();
        _log.WriteLine("=== Coverage by repo ===");
        foreach (var row in repoResults.OrderByDescending(r => r.LinePercent ?? -1))
        {
            var line = row.LinePercent?.ToString("0.0") ?? "—";
            var branch = row.BranchPercent?.ToString("0.0") ?? "—";
            var lines = row.LinesValid > 0 ? $"{row.LinesCovered}/{row.LinesValid}" : "";
            var tests = row.TestsTotal > 0 ? $"{row.TestsPassed}/{row.TestsTotal}" : "";
            _log.WriteLine($"{row.Repo,-24} {row.Status,-6} {line,6} {branch,6} {lines,12} {tests,10} {row.Seconds,7:0.0}");
        }

        _log.WriteLine();
        _log.WriteLine($"Summary:  {mdPath}");
        _log.WriteLine($"JSON:     {jsonPath}");
        if (htmlIndex is not null)
            _log.WriteLine($"HTML:     {htmlIndex}");
        if (aggLine is not null)
            _log.WriteLine($"Aggregate line coverage: {aggLine:0.0}%  branch: {aggBranch:0.0}%");
        _log.WriteLine($"Elapsed: {(DateTime.UtcNow - started).TotalSeconds:0.0}s");

        var gateFailed = false;
        string? gateMessage = null;
        if (failBelow > 0 && aggLine is not null)
        {
            var summary = new CoberturaSummary
            {
                LinePercent = aggLine.Value,
                BranchPercent = aggBranch ?? 100,
                LinesCovered = 0,
                LinesValid = 0,
                BranchesCovered = 0,
                BranchesValid = aggBranch is null ? 0 : 1,
            };
            (gateFailed, gateMessage) = CoverageGate.Evaluate(summary, failBelow);
            if (gateFailed && gateMessage is not null)
                _log.WriteLine(gateMessage);
        }

        var failed = repoResults.Where(r => r.Status == "fail").ToList();
        if (failed.Count > 0)
        {
            _log.WriteLine();
            _log.WriteLine($"FAILED repos ({failed.Count}):");
            foreach (var f in failed)
                _log.WriteLine($"  - {f.Repo}: {f.Error}");
        }

        if (options.OpenReport && htmlIndex is not null && OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = htmlIndex,
                UseShellExecute = true,
            });
        }

        return new CoverageCollectResult
        {
            OutputDir = outputDir,
            HtmlIndexPath = htmlIndex,
            SummaryMarkdownPath = mdPath,
            AggregateLinePercent = aggLine,
            AggregateBranchPercent = aggBranch,
            DurationSeconds = (DateTime.UtcNow - started).TotalSeconds,
            Repos = repoResults,
            GateFailed = gateFailed,
            GateMessage = gateMessage,
        };
    }

    private static async Task RegeneratePlatformSlnxAsync(string root, CancellationToken ct)
    {
        var script = Path.Combine(root, "novolis-governance", "build", "Generate-Platform-Slnx.ps1");
        if (!File.Exists(script))
            throw new FileNotFoundException("Generate-Platform-Slnx.ps1 not found.", script);

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("-WorkspaceRoot");
        psi.ArgumentList.Add(root);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start pwsh");
        _ = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        _ = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"Generate-Platform-Slnx.ps1 failed (exit {p.ExitCode})");
    }

    private static async Task<CoverageRepoResult> CollectRepoAsync(
        CoverageRepo repo,
        string configuration,
        bool skipBuild,
        string projectRef,
        string repoRawDir,
        string logPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(repoRawDir);
        var log = new StringBuilder();
        var sw = Stopwatch.StartNew();
        var cobertura = new List<string>();
        var testsTotal = 0;
        var testsPassed = 0;
        var testsFailed = 0;

        try
        {
            if (!skipBuild)
            {
                foreach (var proj in repo.TestProjects)
                {
                    log.AppendLine($"[{DateTime.Now:HH:mm:ss}] build {Path.GetFileNameWithoutExtension(proj)}");
                    var (exit, output) = await RunDotnetAsync(
                        ["build", proj, "-c", configuration, "--nologo", $"-p:NovolisUseProjectReferences={projectRef}"],
                        repo.Path,
                        cancellationToken).ConfigureAwait(false);
                    log.AppendLine(output);
                    if (exit != 0)
                        throw new InvalidOperationException($"build failed for {Path.GetFileNameWithoutExtension(proj)} (exit {exit})");
                }
            }

            var index = 0;
            foreach (var proj in repo.TestProjects)
            {
                index++;
                var leaf = Path.GetFileNameWithoutExtension(proj);
                var outFile = Path.Combine(repoRawDir, $"{index:D2}-{leaf}.cobertura.xml");
                log.AppendLine($"[{DateTime.Now:HH:mm:ss}] test+coverage {leaf}");

                var args = new List<string>
                {
                    "test",
                    "--project", proj,
                    "-c", configuration,
                    $"-p:NovolisUseProjectReferences={projectRef}",
                };
                if (skipBuild)
                    args.Add("--no-build");
                args.Add("--");
                args.Add("--coverage");
                args.Add("--coverage-output-format");
                args.Add("cobertura");
                args.Add("--coverage-output");
                args.Add(outFile);

                var (exit, output) = await RunDotnetAsync(args, repo.Path, cancellationToken).ConfigureAwait(false);
                log.AppendLine(output);

                var tm = TotalRe.Match(output);
                if (tm.Success)
                    testsTotal += int.Parse(tm.Groups[1].Value);
                var sm = SucceededRe.Match(output);
                if (sm.Success)
                    testsPassed += int.Parse(sm.Groups[1].Value);
                var fm = FailedRe.Match(output);
                if (fm.Success)
                    testsFailed += int.Parse(fm.Groups[1].Value);

                if (exit != 0)
                    throw new InvalidOperationException($"dotnet test failed for {leaf} (exit {exit})");

                if (File.Exists(outFile))
                {
                    cobertura.Add(outFile);
                }
                else
                {
                    var alt = outFile.Replace(".cobertura.xml", "", StringComparison.Ordinal);
                    if (File.Exists(alt))
                    {
                        File.Move(alt, outFile, overwrite: true);
                        cobertura.Add(outFile);
                    }
                    else
                    {
                        log.AppendLine($"[{DateTime.Now:HH:mm:ss}] WARN: coverage file missing for {leaf}");
                    }
                }
            }

            if (cobertura.Count == 0)
                throw new InvalidOperationException("no cobertura files produced");

            await File.WriteAllTextAsync(logPath, log.ToString(), cancellationToken).ConfigureAwait(false);
            return new CoverageRepoResult
            {
                Repo = repo.Name,
                Status = "ok",
                Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
                CoberturaFiles = cobertura,
                TestsTotal = testsTotal,
                TestsPassed = testsPassed,
                TestsFailed = testsFailed,
            };
        }
        catch (Exception ex)
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
            await File.WriteAllTextAsync(logPath, log.ToString(), cancellationToken).ConfigureAwait(false);
            return new CoverageRepoResult
            {
                Repo = repo.Name,
                Status = "fail",
                Error = ex.Message,
                Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1),
                CoberturaFiles = cobertura,
                TestsTotal = testsTotal,
                TestsPassed = testsPassed,
                TestsFailed = testsFailed,
            };
        }
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(
        IReadOnlyList<string> args,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet");
        var stdout = await p.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderr = await p.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (p.ExitCode, stdout + stderr);
    }

    private static void WriteSummaryMarkdown(
        string path,
        string root,
        string? platformSlnx,
        bool platformMode,
        int throttle,
        double failBelow,
        double elapsed,
        double? aggLine,
        double? aggBranch,
        IReadOnlyList<CoverageRepoResult> repos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Novolis coverage report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var mode = platformMode ? "Platform.slnx ProjectRef" : "NuGet per-repo";
        sb.AppendLine($"Duration: {elapsed:0.0}s  |  Repos: {repos.Count}  |  Throttle: {throttle}  |  Mode: {mode}");
        if (platformSlnx is not null)
            sb.AppendLine($"Solution: {platformSlnx}");
        if (aggLine is not null)
            sb.AppendLine($"**Aggregate line: {aggLine:0.0}%**  |  **branch: {aggBranch:0.0}%**");
        sb.AppendLine();
        sb.AppendLine("| Repo | Status | Line % | Branch % | Lines | Tests | Seconds |");
        sb.AppendLine("|------|--------|--------|----------|-------|-------|---------|");
        foreach (var row in repos.OrderBy(r => r.Repo, StringComparer.OrdinalIgnoreCase))
        {
            var line = row.LinePercent?.ToString("0.0") ?? "—";
            var branch = row.BranchPercent?.ToString("0.0") ?? "—";
            var lines = row.LinesValid > 0 ? $"{row.LinesCovered}/{row.LinesValid}" : "";
            var tests = row.TestsTotal > 0 ? $"{row.TestsPassed}/{row.TestsTotal}" : "";
            sb.AppendLine($"| {row.Repo} | {row.Status} | {line} | {branch} | {lines} | {tests} | {row.Seconds:0.0} |");
        }

        var failed = repos.Where(r => r.Status == "fail").ToList();
        if (failed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Failures");
            foreach (var f in failed)
                sb.AppendLine($"- **{f.Repo}**: {f.Error} (log: `logs/{f.Repo}.log`)");
        }

        sb.AppendLine();
        sb.AppendLine("HTML report: [report/index.html](report/index.html)");
        _ = root;
        _ = failBelow;
        File.WriteAllText(path, sb.ToString());
    }

    private static string BuildSummaryJson(
        bool platformMode,
        string? platformSlnx,
        double failBelow,
        double elapsed,
        int throttle,
        double? aggLine,
        double? aggBranch,
        IReadOnlyList<CoverageRepoResult> repos)
    {
        // Minimal hand-rolled JSON to avoid extra package deps.
        static string Esc(string? s) =>
            (s ?? "").Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"generatedUtc\":\"{DateTime.UtcNow:o}\",");
        sb.Append($"\"durationSeconds\":{elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
        sb.Append($"\"throttleLimit\":{throttle},");
        sb.Append($"\"mode\":\"{(platformMode ? "Platform.slnx ProjectRef" : "NuGet per-repo")}\",");
        sb.Append($"\"platformSlnx\":\"{Esc(platformSlnx)}\",");
        sb.Append($"\"failBelow\":{failBelow.ToString(System.Globalization.CultureInfo.InvariantCulture)},");
        sb.Append($"\"aggregateLine\":{(aggLine?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")},");
        sb.Append($"\"aggregateBranch\":{(aggBranch?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")},");
        sb.Append("\"repos\":[");
        for (var i = 0; i < repos.Count; i++)
        {
            var r = repos[i];
            if (i > 0)
                sb.Append(',');
            sb.Append('{');
            sb.Append($"\"repo\":\"{Esc(r.Repo)}\",\"status\":\"{Esc(r.Status)}\",");
            sb.Append($"\"linePercent\":{(r.LinePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")},");
            sb.Append($"\"branchPercent\":{(r.BranchPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null")},");
            sb.Append($"\"seconds\":{r.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            sb.Append('}');
        }

        sb.Append("]}");
        return sb.ToString();
    }
}
