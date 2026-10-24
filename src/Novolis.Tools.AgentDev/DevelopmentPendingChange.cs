namespace Novolis.Tools.AgentDev;

public sealed class DevelopmentPendingChange
{
    public string CorrelationId { get; init; } = "";

    public string Path { get; init; } = "";

    public string Content { get; init; } = "";
}
