# novolis-agent-dev

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.AgentDev.Cli\Novolis.Tools.AgentDev.Cli.csproj -- --root d:\work --mcp
```

`--mcp` serves the development profile on stdio as an MCP server. `--stdio` serves newline-delimited JSON. Without either flag the command prints one snapshot and exits.
