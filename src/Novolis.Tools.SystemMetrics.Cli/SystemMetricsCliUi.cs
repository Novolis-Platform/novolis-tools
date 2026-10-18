using System.Globalization;
using Novolis.IO.SystemMetrics;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Novolis.Tools.SystemMetrics.Cli;

internal static class SystemMetricsCliUi
{
    public static bool CanUseInteractiveConsole =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static void WriteInventory(
        IAnsiConsole console,
        SystemMetricsInventory inventory)
    {
        console.Write(
            new Panel(
                    $"[grey]Host[/] {Escape(inventory.Host.MachineName)}\n" +
                    $"[grey]OS[/] {Escape(inventory.Host.OperatingSystem)}\n" +
                    $"[grey]Architecture[/] {Escape(inventory.Host.Architecture)}\n" +
                    $"[grey]CPU[/] {Escape(inventory.Cpu.Name)} " +
                    $"({inventory.Cpu.LogicalProcessorCount} logical processors)")
                .Header("[cyan]System inventory[/]")
                .RoundedBorder()
                .Expand());
        console.WriteLine();
        WriteGpuTable(console, inventory.GpuAdapters);
        WriteNetworkTable(console, inventory.NetworkAdapters);
        WriteScreenTable(console, inventory.Screens);
        WriteDiagnostics(console, inventory.Diagnostics);
    }

    public static IRenderable BuildLive(
        SystemMetricsInventory inventory,
        SystemMetricsSample? sample,
        long sampleCount)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();
        table.AddColumn("Resource");
        table.AddColumn("Value");
        table.AddColumn("Status");

        if (sample is null)
        {
            table.AddRow("Capture", "Waiting for first sample", "starting");
        }
        else
        {
            table.AddRow(
                "CPU",
                FormatPercent(sample.Cpu.UtilizationPercent),
                Escape(sample.Cpu.Status.ToString()));
            table.AddRow(
                "Memory",
                $"{FormatBytes(sample.Memory.UsedBytes)} / {FormatBytes(sample.Memory.TotalBytes)}",
                Escape(sample.Memory.Status.ToString()));
            if (sample.Process is { } process)
            {
                table.AddRow(
                    $"Process {process.ProcessId}",
                    $"{FormatPercent(process.CpuUtilizationPercent)} CPU, " +
                    $"{FormatBytes(process.WorkingSetBytes)} working set",
                    Escape(process.Status.ToString()));
            }

            foreach (var gpu in sample.Gpus)
            {
                table.AddRow(
                    $"GPU {gpu.AdapterId}",
                    $"{FormatPercent(gpu.UtilizationPercent)} / " +
                    $"{FormatBytes(gpu.DedicatedMemoryUsedBytes)} VRAM",
                    Escape(gpu.Status.ToString()));
            }

            foreach (var network in sample.Networks)
            {
                table.AddRow(
                    $"NET {network.AdapterId}",
                    $"{FormatRate(network.ReceiveBytesPerSecond)} down / " +
                    $"{FormatRate(network.SendBytesPerSecond)} up",
                    Escape(network.Status.ToString()));
            }

            table.AddRow(
                "Screens",
                sample.Screens is null
                    ? $"topology v{sample.ScreenTopologyVersion}"
                    : $"{sample.Screens.Count} screen(s), topology v{sample.ScreenTopologyVersion}",
                "Supported");
            table.AddRow(
                "Samples",
                $"{sampleCount} at {sample.Elapsed.TotalSeconds:0.0}s",
                "capturing");
        }

        return new Panel(table)
            .Header("[cyan]novolis-system-metrics[/]")
            .RoundedBorder()
            .Expand();
    }

    public static void WriteSummary(
        IAnsiConsole console,
        SystemMetricsSummary summary)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Expand();
        table.AddColumn("Metric");
        table.AddColumn("Count");
        table.AddColumn("Min");
        table.AddColumn("Average");
        table.AddColumn("P50");
        table.AddColumn("P95");
        table.AddColumn("Max");

        AddStatistics(table, "CPU %", summary.CpuUtilization, FormatPercent);
        AddStatistics(table, "Process CPU %", summary.ProcessCpuUtilization, FormatPercent);
        foreach (var pair in summary.GpuUtilization.OrderBy(pair => pair.Key))
            AddStatistics(table, $"GPU {pair.Key} %", pair.Value, FormatNumber);
        foreach (var pair in summary.NetworkReceiveBytesPerSecond.OrderBy(pair => pair.Key))
            AddStatistics(table, $"NET {pair.Key} down", pair.Value, FormatRate);
        foreach (var pair in summary.NetworkSendBytesPerSecond.OrderBy(pair => pair.Key))
            AddStatistics(table, $"NET {pair.Key} up", pair.Value, FormatRate);

        if (table.Rows.Count == 0)
            table.AddRow("No numeric metrics", "0", "—", "—", "—", "—", "—");

        console.Write(
            new Panel(table)
                .Header(
                    $"[cyan]Capture summary[/] " +
                    $"[grey]{summary.Duration.TotalSeconds:0.0}s, " +
                    $"{summary.SampleCount} samples[/]")
                .RoundedBorder()
                .Expand());
        console.WriteLine();
        if (summary.Markers.Count > 0)
        {
            var markerTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Marker")
                .AddColumn("Elapsed")
                .AddColumn("Resource");
            foreach (var marker in summary.Markers)
            {
                markerTable.AddRow(
                    Escape(marker.Name),
                    $"{marker.Elapsed.TotalSeconds:0.000}s",
                    Escape(marker.ResourceId ?? "—"));
            }

            console.Write(
                new Panel(markerTable)
                    .Header("[cyan]Markers[/]")
                    .RoundedBorder()
                    .Expand());
            console.WriteLine();
        }

        WriteDiagnostics(console, summary.Diagnostics);
    }

    private static void WriteGpuTable(
        IAnsiConsole console,
        IReadOnlyList<GpuAdapterInfo> adapters)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Adapter")
            .AddColumn("Vendor")
            .AddColumn("Backend")
            .AddColumn("Dedicated");
        foreach (var adapter in adapters)
        {
            table.AddRow(
                Escape(adapter.Id),
                Escape(adapter.Name),
                Escape(adapter.Vendor ?? "—"),
                Escape(adapter.Backend ?? "—"),
                Escape(FormatBytes(adapter.DedicatedMemoryBytes)));
        }

        if (adapters.Count == 0)
            table.AddRow("—", "No adapters reported", "—", "—", "—");
        console.Write(new Panel(table).Header("[cyan]GPU adapters[/]").RoundedBorder().Expand());
        console.WriteLine();
    }

    private static void WriteNetworkTable(
        IAnsiConsole console,
        IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Adapter")
            .AddColumn("Type")
            .AddColumn("Status")
            .AddColumn("Link");
        foreach (var adapter in adapters)
        {
            table.AddRow(
                Escape(adapter.Id),
                Escape(adapter.Name),
                Escape(adapter.InterfaceType.ToString()),
                Escape(adapter.Status.ToString()),
                Escape(FormatRate(adapter.LinkSpeedBitsPerSecond / 8)));
        }

        if (adapters.Count == 0)
            table.AddRow("—", "No adapters reported", "—", "—", "—");
        console.Write(new Panel(table).Header("[cyan]Network adapters[/]").RoundedBorder().Expand());
        console.WriteLine();
    }

    private static void WriteScreenTable(
        IAnsiConsole console,
        IReadOnlyList<ScreenInfo> screens)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Bounds")
            .AddColumn("DPI")
            .AddColumn("Refresh")
            .AddColumn("Primary");
        foreach (var screen in screens)
        {
            table.AddRow(
                Escape(screen.Id),
                $"{screen.Width}x{screen.Height} @ {screen.Left},{screen.Top}",
                Escape(screen.Dpi?.ToString(CultureInfo.InvariantCulture) ?? "—"),
                Escape(screen.RefreshRateHertz is { } refresh
                    ? $"{refresh:0.##} Hz"
                    : "—"),
                screen.IsPrimary ? "[green]yes[/]" : "—");
        }

        if (screens.Count == 0)
            table.AddRow("—", "No screens reported", "—", "—", "—");
        console.Write(new Panel(table).Header("[cyan]Screens[/]").RoundedBorder().Expand());
        console.WriteLine();
    }

    private static void WriteDiagnostics(
        IAnsiConsole console,
        IReadOnlyList<SystemMetricDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            console.MarkupLine(
                $"[yellow]{Escape(diagnostic.Status.ToString())}[/] " +
                $"{Escape(diagnostic.Source)}: {Escape(diagnostic.Message)}");
        }
    }

    private static void AddStatistics(
        Table table,
        string name,
        MetricStatistics? statistics,
        Func<double?, string> formatter)
    {
        if (statistics is null)
            return;
        table.AddRow(
            Escape(name),
            statistics.Count.ToString(CultureInfo.InvariantCulture),
            formatter(statistics.Minimum),
            formatter(statistics.Average),
            formatter(statistics.P50),
            formatter(statistics.P95),
            formatter(statistics.Maximum));
    }

    private static string FormatPercent(double? value) =>
        value is { } number
            ? $"{number.ToString("0.##", CultureInfo.InvariantCulture)}%"
            : "—";

    private static string FormatNumber(double? value) =>
        value is { } number
            ? number.ToString("0.##", CultureInfo.InvariantCulture)
            : "—";

    private static string FormatRate(long? bytesPerSecond) =>
        bytesPerSecond is { } value
            ? $"{FormatBytes(value)}/s"
            : "—";

    private static string FormatRate(double? bytesPerSecond) =>
        bytesPerSecond is { } value
            ? $"{FormatBytes((long)Math.Max(0, value))}/s"
            : "—";

    private static string FormatBytes(long? bytes) =>
        bytes is not { } value
            ? "—"
            : FormatBytes(value);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.##} KiB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / 1024d / 1024d:0.##} MiB";
        return $"{bytes / 1024d / 1024d / 1024d:0.##} GiB";
    }

    private static string Escape(string? value) =>
        Markup.Escape(value ?? string.Empty);
}
