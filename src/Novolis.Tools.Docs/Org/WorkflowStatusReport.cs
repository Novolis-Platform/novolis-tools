using System.Globalization;
using System.Text.Json;

namespace Novolis.Tools.Docs.Org;

/// <summary>Prints recent GitHub Actions runs and the first error from each failed job.</summary>
public static class WorkflowStatusReport
{
    /// <summary>Writes the status summary to stdout. Returns 1 when any listed run failed.</summary>
    public static int Run(WorkflowStatusOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Repo) || !options.Repo.Contains('/', StringComparison.Ordinal))
            throw new ArgumentException("Repo must be owner/name.", nameof(options));

        var limit = Math.Clamp(options.Limit, 1, 30);
        string runsJson;
        try
        {
            runsJson = GhProcess.RunGh(
            [
                "run", "list",
                "--repo", options.Repo,
                "--limit", limit.ToString(CultureInfo.InvariantCulture),
                "--json", "databaseId,name,displayTitle,status,conclusion,url,createdAt",
            ]);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message.Split('\n')[0]);
            return 1;
        }

        using var runs = JsonDocument.Parse(runsJson);
        var failedRuns = 0;
        foreach (var run in runs.RootElement.EnumerateArray())
        {
            var conclusion = Text(run, "conclusion");
            if (string.IsNullOrWhiteSpace(conclusion))
                conclusion = Text(run, "status");

            var title = Text(run, "displayTitle");
            if (title.Length > 72)
                title = title[..72] + "…";

            Console.WriteLine($"{Text(run, "name"),-12} {conclusion,-10} {title}");
            Console.WriteLine($"             {Text(run, "url")}");

            if (conclusion is not ("failure" or "cancelled"))
                continue;

            failedRuns++;
            try
            {
                PrintFailedJobs(options.Repo, run.GetProperty("databaseId").GetInt64());
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"  jobs unavailable: {ex.Message.Split('\n')[0]}");
            }
        }

        Console.WriteLine(failedRuns == 0
            ? "No failed runs in this window."
            : $"{failedRuns} failed or cancelled run(s).");
        return failedRuns == 0 ? 0 : 1;
    }

    static void PrintFailedJobs(string repo, long runId)
    {
        var jobsJson = GhProcess.RunGh(
        [
            "run", "view", runId.ToString(CultureInfo.InvariantCulture),
            "--repo", repo,
            "--json", "jobs",
        ]);
        using var jobsDoc = JsonDocument.Parse(jobsJson);
        foreach (var job in jobsDoc.RootElement.GetProperty("jobs").EnumerateArray())
        {
            var conclusion = Text(job, "conclusion");
            if (conclusion is not ("failure" or "cancelled"))
                continue;

            var name = Text(job, "name");
            Console.WriteLine($"  {conclusion}  {name}");
            var error = FirstError(repo, job.GetProperty("databaseId").GetInt64());
            if (!string.IsNullOrWhiteSpace(error))
                Console.WriteLine($"    {error}");
        }
    }

    static string? FirstError(string repo, long jobId)
    {
        var log = GhProcess.RunGh(
            ["api", $"repos/{repo}/actions/jobs/{jobId.ToString(CultureInfo.InvariantCulture)}/logs"],
            ignoreFailure: true);
        foreach (var raw in log.Split('\n'))
        {
            var line = StripStamp(raw.Trim());
            if (line.Length == 0 || line.Contains("Process completed with exit code", StringComparison.Ordinal))
                continue;

            if (line.Contains("##[error]", StringComparison.Ordinal)
                || line.Contains("error CS", StringComparison.Ordinal)
                || line.Contains("error MSB", StringComparison.Ordinal)
                || line.Contains("Test run summary: Failed", StringComparison.Ordinal)
                || line.Contains("AssertionException", StringComparison.Ordinal))
            {
                const string marker = "##[error]";
                var index = line.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0)
                    line = line[(index + marker.Length)..].Trim();
                return line.Length <= 180 ? line : line[..180] + "…";
            }
        }

        return null;
    }

    static string StripStamp(string line)
    {
        var zone = line.IndexOf('Z');
        if (zone is > 18 and < 40 && line.StartsWith("20", StringComparison.Ordinal))
            return line[(zone + 1)..].Trim();
        return line;
    }

    static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
