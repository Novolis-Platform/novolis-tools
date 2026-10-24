using System.CommandLine;
using System.Text.Json;
using Novolis.Agent.Surface;
using Novolis.Tools.AgentDev;

var rootOption = new Option<string>("--root")
{
    Description = "Workspace directory the profile may inspect.",
    Required = true,
};
var mcpOption = new Option<bool>("--mcp")
{
    Description = "Serve Model Context Protocol on stdio.",
};
var stdioOption = new Option<bool>("--stdio")
{
    Description = "Serve newline-delimited agent JSON on stdio.",
};

var command = new RootCommand("Development, debug, and test agent profile.");
command.Options.Add(rootOption);
command.Options.Add(mcpOption);
command.Options.Add(stdioOption);
command.SetAction(async (parse, cancellationToken) =>
{
    var root = parse.GetValue(rootOption) ?? ".";
    var host = new DevelopmentAgentHost(Path.GetFullPath(root), new DotNetProcessRunner());
    var definition = AgentSurfaceDefinition.From<IDevelopmentSurface>();
    if (parse.GetValue(mcpOption))
    {
        await using var mcp = new AgentMcpStdioTransport(host, definition);
        await mcp.RunUntilEofAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    if (parse.GetValue(stdioOption))
    {
        await using var stdio = new AgentStdioHost(host);
        await stdio.RunUntilEofAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    Console.WriteLine(JsonSerializer.Serialize(host.Snapshot(), AgentJson.Options));
    return 0;
});

return await command.Parse(args).InvokeAsync().ConfigureAwait(false);
