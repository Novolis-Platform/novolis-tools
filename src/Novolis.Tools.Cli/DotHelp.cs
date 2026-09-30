namespace Novolis.Tools.Cli;

/// <summary>One dot-command help entry.</summary>
/// <param name="Name">Command name including leading dot.</param>
/// <param name="Synopsis">One-line summary.</param>
/// <param name="Details">Longer description.</param>
/// <param name="Example">Example invocation.</param>
public sealed record DotHelpEntry(string Name, string Synopsis, string Details, string Example);
