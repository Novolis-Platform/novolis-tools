namespace Novolis.Tools.AgentDev;

public sealed class DevelopmentProcessResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = "";

    public string StandardError { get; init; } = "";
}
