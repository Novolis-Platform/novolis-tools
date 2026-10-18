using System.Text.Json;
using Novolis.IO.SystemMetrics;
using Novolis.Tools.Cli;
using Spectre.Console;

namespace Novolis.Tools.SystemMetrics.Cli;

internal static class SystemMetricsCliRunner
{
    public static async Task<int> RunInventoryAsync(
        IAnsiConsole console,
        bool json,
        CancellationToken cancellationToken)
    {
        try
        {
            var monitor = SystemMetricsMonitor.CreateDefault();
            await using var capture = await monitor
                .StartAsync(
                    new SystemMetricsCaptureOptions
                    {
                        SampleInterval = TimeSpan.FromMilliseconds(250),
                        MaximumSamples = 1,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (json)
            {
                WriteJson(
                    console,
                    new { inventory = ToInventoryPayload(capture.Inventory) });
            }
            else
            {
                SystemMetricsCliUi.WriteInventory(console, capture.Inventory);
            }

            await capture.CompleteAsync().ConfigureAwait(false);
            return ExitCodes.Ok;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Failure;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ExitCodes.Failure;
        }
    }

    public static async Task<int> RunCaptureAsync(
        IAnsiConsole console,
        int intervalMilliseconds,
        int durationSeconds,
        int? processId,
        string? selectedGpuAdapterId,
        bool json,
        CancellationToken cancellationToken)
    {
        if (intervalMilliseconds <= 0)
        {
            console.MarkupLine("[red]--interval must be greater than zero.[/]");
            return ExitCodes.Usage;
        }
        if (durationSeconds <= 0)
        {
            console.MarkupLine("[red]--duration must be greater than zero.[/]");
            return ExitCodes.Usage;
        }
        if (processId is <= 0)
        {
            console.MarkupLine("[red]--pid must be positive.[/]");
            return ExitCodes.Usage;
        }

        try
        {
            var monitor = SystemMetricsMonitor.CreateDefault();
            await using var capture = await monitor
                .StartAsync(
                    new SystemMetricsCaptureOptions
                    {
                        SampleInterval = TimeSpan.FromMilliseconds(intervalMilliseconds),
                        ProcessId = processId,
                        SelectedGpuAdapterId = selectedGpuAdapterId,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            capture.Mark("capture-start", selectedGpuAdapterId);

            var selectedGpuAdapterFound =
                string.IsNullOrWhiteSpace(selectedGpuAdapterId)
                || capture.Inventory.GpuAdapters.Any(
                    adapter => string.Equals(
                        adapter.Id,
                        selectedGpuAdapterId,
                        StringComparison.Ordinal));
            if (!selectedGpuAdapterFound && !json)
            {
                console.MarkupLine(
                    $"[yellow]Adapter id was not found in inventory: " +
                    $"{Markup.Escape(selectedGpuAdapterId ?? string.Empty)}[/]");
            }

            SystemMetricsSample? latest = null;
            long sampleCount = 0;
            SystemMetricsSummary? summary = null;
            var cancelled = false;

            if (!json && SystemMetricsCliUi.CanUseInteractiveConsole)
            {
                await console
                    .Live(SystemMetricsCliUi.BuildLive(capture.Inventory, null, 0))
                    .StartAsync(async context =>
                    {
                        var consumer = ConsumeSamplesAsync(
                            capture,
                            sample =>
                            {
                                latest = sample;
                                sampleCount++;
                                context.UpdateTarget(
                                    SystemMetricsCliUi.BuildLive(
                                        capture.Inventory,
                                        latest,
                                        sampleCount));
                            });
                        cancelled = await WaitForDurationAsync(
                                durationSeconds,
                                cancellationToken)
                            .ConfigureAwait(false);
                        capture.Mark("capture-end", selectedGpuAdapterId);
                        summary = await capture.CompleteAsync().ConfigureAwait(false);
                        await consumer.ConfigureAwait(false);
                    })
                    .ConfigureAwait(false);
            }
            else
            {
                var consumer = ConsumeSamplesAsync(
                    capture,
                    sample =>
                    {
                        latest = sample;
                        sampleCount++;
                    });
                cancelled = await WaitForDurationAsync(
                        durationSeconds,
                        cancellationToken)
                    .ConfigureAwait(false);
                capture.Mark("capture-end", selectedGpuAdapterId);
                summary = await capture.CompleteAsync().ConfigureAwait(false);
                await consumer.ConfigureAwait(false);
            }

            if (json)
            {
                WriteJson(
                    console,
                    new
                    {
                        inventory = ToInventoryPayload(capture.Inventory),
                        summary,
                        lastSample = latest,
                        selectedGpuAdapterId,
                        selectedGpuAdapterFound,
                    });
            }
            else
            {
                if (cancelled)
                    console.MarkupLine("[yellow]Capture cancelled.[/]");
                SystemMetricsCliUi.WriteSummary(console, summary!);
            }

            return cancelled ? ExitCodes.Failure : ExitCodes.Ok;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Failure;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ExitCodes.Failure;
        }
    }

    private static async Task ConsumeSamplesAsync(
        SystemMetricsCapture capture,
        Action<SystemMetricsSample> onSample)
    {
        await foreach (var sample in capture.Samples.ConfigureAwait(false))
            onSample(sample);
    }

    private static async Task<bool> WaitForDurationAsync(
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                    TimeSpan.FromSeconds(durationSeconds),
                    cancellationToken)
                .ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private static void WriteJson(IAnsiConsole console, object value) =>
        console.WriteLine(
            JsonSerializer.Serialize(
                value,
                new JsonSerializerOptions { WriteIndented = true }));

    private static object ToInventoryPayload(SystemMetricsInventory inventory) =>
        new
        {
            host = inventory.Host,
            cpu = inventory.Cpu,
            gpuAdapters = inventory.GpuAdapters,
            networkAdapters = inventory.NetworkAdapters.Select(adapter => new
            {
                adapter.Id,
                adapter.Name,
                adapter.Description,
                interfaceType = adapter.InterfaceType.ToString(),
                status = adapter.Status.ToString(),
                adapter.LinkSpeedBitsPerSecond,
                addresses = adapter.Addresses.Select(address => address.ToString()).ToArray(),
                gateways = adapter.Gateways.Select(gateway => gateway.ToString()).ToArray(),
            }),
            screens = inventory.Screens,
            diagnostics = inventory.Diagnostics,
        };
}
