using System.Net.Sockets;
using Novolis.Transports.NetworkPath;

namespace Novolis.Tools.NetworkPath.Cli;

internal static class NetworkPathCliRunner
{
    public static async Task<int> RunInspectAsync(
        string? targetText,
        int count,
        double timeoutSeconds,
        bool allAdapters,
        bool interactive,
        string format,
        bool showAddresses,
        CancellationToken cancellationToken)
    {
        if (count is < 1 or > 100)
        {
            Console.Error.WriteLine("--count must be between 1 and 100.");
            return 2;
        }

        if (timeoutSeconds <= 0 || timeoutSeconds > 120)
        {
            Console.Error.WriteLine("--timeout must be greater than 0 and no more than 120 seconds.");
            return 2;
        }

        var json = format.Equals("json", StringComparison.OrdinalIgnoreCase);
        if (!json &&
            !format.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--format must be table or json.");
            return 2;
        }

        var canPrompt = interactive && NetworkPathCliUi.CanUseInteractiveConsole;
        NetworkProbeTarget? target = null;
        if (!string.IsNullOrWhiteSpace(targetText))
        {
            if (!NetworkProbeTarget.TryParse(targetText, out target, out var targetError))
            {
                Console.Error.WriteLine(targetError);
                return 2;
            }
        }
        else if (canPrompt)
        {
            if (!json)
                NetworkPathCliUi.ShowTitle();
            target = NetworkPathCliUi.PromptTarget();
        }

        if (target is null)
        {
            Console.Error.WriteLine(
                "An explicit --target host:port is required when interactive prompts are unavailable.");
            return 2;
        }

        var request = new NetworkProbeRequest(
            target,
            count,
            TimeSpan.FromSeconds(timeoutSeconds),
            allAdapters);
        var inspector = new NetworkPathInspector();
        try
        {
            var report = canPrompt && !json
                ? await NetworkPathCliUi.RunStatusAsync(
                    $"Inspecting {target}...",
                    () => inspector.InspectAsync(request, cancellationToken))
                : await inspector.InspectAsync(request, cancellationToken);

            if (json)
                NetworkPathCliUi.WriteJson(report, showAddresses);
            else if (canPrompt)
                NetworkPathCliUi.WriteHuman(report, showAddresses);
            else
                NetworkPathCliUi.WritePlain(report, showAddresses);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("The network path inspection was cancelled.");
            return 1;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or SocketException)
        {
            Console.Error.WriteLine($"The network path inspection failed: {exception.Message}");
            return 1;
        }
    }
}
