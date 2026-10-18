using System.CommandLine;
using Spectre.Console;
using Novolis.Tools.SystemMetrics.Cli;

var jsonInventoryOption = new Option<bool>("--json")
{
    Description = "Write one JSON document instead of the TUI.",
    DefaultValueFactory = _ => false,
};

var inventory = new Command(
    "inventory",
    "Show CPU, GPU, network-adapter, and screen inventory.")
{
    jsonInventoryOption,
};
inventory.SetAction(
    (parseResult, cancellationToken) =>
        SystemMetricsCliRunner.RunInventoryAsync(
            AnsiConsole.Console,
            parseResult.GetValue(jsonInventoryOption),
            cancellationToken));

var intervalOption = new Option<int>("--interval")
{
    Description = "Sampling interval in milliseconds (default 500).",
    DefaultValueFactory = _ => 500,
};
var durationOption = new Option<int>("--duration")
{
    Description = "Capture duration in seconds (default 10).",
    DefaultValueFactory = _ => 10,
};
var processIdOption = new Option<int?>("--pid")
{
    Description = "Process id to measure in addition to system resources.",
};
var adapterOption = new Option<string?>("--adapter")
{
    Description = "Selected GPU adapter id to attach to capture markers.",
};
var jsonCaptureOption = new Option<bool>("--json")
{
    Description = "Write one JSON summary instead of the live TUI.",
    DefaultValueFactory = _ => false,
};

var capture = new Command(
    "capture",
    "Capture live system metrics for a bounded time span.")
{
    intervalOption,
    durationOption,
    processIdOption,
    adapterOption,
    jsonCaptureOption,
};
capture.SetAction(
    (parseResult, cancellationToken) =>
        SystemMetricsCliRunner.RunCaptureAsync(
            AnsiConsole.Console,
            parseResult.GetValue(intervalOption),
            parseResult.GetValue(durationOption),
            parseResult.GetValue(processIdOption),
            parseResult.GetValue(adapterOption),
            parseResult.GetValue(jsonCaptureOption),
            cancellationToken));

var root = new RootCommand("""
    novolis-system-metrics — inspect and capture host resource usage

    Examples:
      novolis-system-metrics inventory
      novolis-system-metrics inventory --json
      novolis-system-metrics capture --pid 1234 --duration 30
      novolis-system-metrics capture --interval 250 --adapter windows-luid-...
      novolis-system-metrics capture --duration 10 --json
    """)
{
    inventory,
    capture,
};

return await root.Parse(args).InvokeAsync();
