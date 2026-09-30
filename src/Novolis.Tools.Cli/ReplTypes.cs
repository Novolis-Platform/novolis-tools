namespace Novolis.Tools.Cli;

/// <summary>Process exit codes used by Novolis tools.</summary>
public static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>Command or query failed.</summary>
    public const int Failure = 1;

    /// <summary>Invalid arguments / usage.</summary>
    public const int Usage = 2;

    /// <summary>REPL requests exit (internal).</summary>
    public const int Quit = 99;
}
