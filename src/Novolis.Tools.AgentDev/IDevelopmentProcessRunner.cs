namespace Novolis.Tools.AgentDev;

public interface IDevelopmentProcessRunner
{
    DevelopmentProcessResult Run(string verb, string workingDirectory, CancellationToken cancellationToken);
}
