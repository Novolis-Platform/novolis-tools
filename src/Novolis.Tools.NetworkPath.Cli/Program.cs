using System.CommandLine;
using Novolis.Tools.NetworkPath.Cli;

var targetOption = new Option<string?>("--target")
{
    Description = "TCP probe target in host:port form",
};
var countOption = new Option<int>("--count")
{
    Description = "Connection attempts per adapter (default 3)",
    DefaultValueFactory = _ => 3,
};
var timeoutOption = new Option<double>("--timeout")
{
    Description = "Timeout per adapter attempt in seconds (default 3)",
    DefaultValueFactory = _ => 3d,
};
var allAdaptersOption = new Option<bool>("--all-adapters")
{
    Description = "Probe every operational adapter with a local address",
    DefaultValueFactory = _ => false,
};
var formatOption = new Option<string>("--format")
{
    Description = "Output format: table or json",
    DefaultValueFactory = _ => "table",
};
var interactiveOption = new Option<bool>("--interactive")
{
    Description = "Use Spectre.Console prompts and panels in a real terminal",
    DefaultValueFactory = _ => false,
};
var showAddressesOption = new Option<bool>("--show-addresses")
{
    Description = "Include full local addresses in diagnostic output",
    DefaultValueFactory = _ => false,
};

var inspect = new Command("inspect", "Inspect adapters and compare bound network paths")
{
    targetOption,
    countOption,
    timeoutOption,
    allAdaptersOption,
    formatOption,
    interactiveOption,
    showAddressesOption
};

inspect.SetAction(async (parseResult, cancellationToken) =>
{
    return await NetworkPathCliRunner.RunInspectAsync(
        parseResult.GetValue(targetOption),
        parseResult.GetValue(countOption),
        parseResult.GetValue(timeoutOption),
        parseResult.GetValue(allAdaptersOption),
        parseResult.GetValue(interactiveOption),
        parseResult.GetValue(formatOption) ?? "table",
        parseResult.GetValue(showAddressesOption),
        cancellationToken);
});

var root = new RootCommand("""
    novolis-network-path — inspect local egress paths and adapter-bound TCP probes

    Examples:
      novolis-network-path inspect --target example.com:443
      novolis-network-path inspect --target example.com:443 --all-adapters --count 5
      novolis-network-path inspect --target example.com:443 --format json
    """)
{
    inspect
};

return await root.Parse(args).InvokeAsync();
