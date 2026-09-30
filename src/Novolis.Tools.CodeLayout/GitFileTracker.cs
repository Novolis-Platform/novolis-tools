using System.ComponentModel;
using System.Diagnostics;

namespace Novolis.Tools.CodeLayout;

internal sealed class GitFileTracker : IGitFileTracker
{
    public async ValueTask<GitTrackingResult> TrackAsync(
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken)
    {
        var changes = new List<CodeLayoutChange>();
        var diagnostics = new List<CodeLayoutDiagnostic>();
        var repositoryFiles = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory is null)
            {
                AddRepositoryWarning(
                    diagnostics,
                    fullPath,
                    "The new file does not have a directory from which Git can be located.");
                continue;
            }

            GitProcessResult result;
            try
            {
                result = await RunGitAsync(
                        directory,
                        ["rev-parse", "--show-toplevel"],
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Win32Exception exception)
            {
                AddRepositoryWarning(
                    diagnostics,
                    fullPath,
                    $"Git could not be started: {exception.Message}");
                continue;
            }
            catch (InvalidOperationException exception)
            {
                AddRepositoryWarning(diagnostics, fullPath, exception.Message);
                continue;
            }

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "The file is not inside a Git repository."
                    : result.StandardError.Trim();
                AddRepositoryWarning(diagnostics, fullPath, detail);
                continue;
            }

            var repositoryRoot = Path.GetFullPath(result.StandardOutput.Trim());
            if (!repositoryFiles.TryGetValue(repositoryRoot, out var files))
            {
                files = [];
                repositoryFiles.Add(repositoryRoot, files);
            }

            files.Add(fullPath);
        }

        foreach (var repository in repositoryFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePaths = repository.Value
                .Select(path => Path.GetRelativePath(repository.Key, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            GitProcessResult result;
            try
            {
                result = await RunGitAsync(
                        repository.Key,
                        ["add", "--", .. relativePaths],
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Win32Exception exception)
            {
                diagnostics.Add(new CodeLayoutDiagnostic(
                    "NCL2102",
                    $"Git could not stage newly created files: {exception.Message}",
                    "error",
                    repository.Key));
                continue;
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Add(new CodeLayoutDiagnostic(
                    "NCL2102",
                    exception.Message,
                    "error",
                    repository.Key));
                continue;
            }

            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Git could not stage newly created files."
                    : result.StandardError.Trim();
                diagnostics.Add(new CodeLayoutDiagnostic(
                    "NCL2102",
                    detail,
                    "error",
                    repository.Key));
                continue;
            }

            changes.Add(new CodeLayoutChange(
                "git-staged",
                repository.Key,
                $"Staged {relativePaths.Length} newly created file(s)."));
        }

        return new GitTrackingResult(changes, diagnostics);
    }

    private static void AddRepositoryWarning(
        ICollection<CodeLayoutDiagnostic> diagnostics,
        string filePath,
        string detail) =>
        diagnostics.Add(new CodeLayoutDiagnostic(
            "NCL2101",
            detail,
            "warning",
            filePath));

    private static async ValueTask<GitProcessResult> RunGitAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Git did not start.");

        try
        {
            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return new GitProcessResult(
                process.ExitCode,
                await standardOutput.ConfigureAwait(false),
                await standardError.ConfigureAwait(false));
        }
        catch
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private sealed record GitProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
