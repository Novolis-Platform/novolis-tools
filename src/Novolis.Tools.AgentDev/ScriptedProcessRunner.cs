namespace Novolis.Tools.AgentDev;

public sealed class ScriptedProcessRunner : IDevelopmentProcessRunner
{
    private readonly Queue<DevelopmentProcessResult> _results = new();

    public void Enqueue(DevelopmentProcessResult result) => _results.Enqueue(result);

    public DevelopmentProcessResult Run(string verb, string workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _results.Count == 0
            ? new DevelopmentProcessResult()
            : _results.Dequeue();
    }
}
