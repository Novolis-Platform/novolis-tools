using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Novolis.Tools.Coverage;

/// <summary>One scored method.</summary>
public sealed class CrapMethodEntry
{
    /// <summary>Underlying Cobertura method.</summary>
    public required CoberturaMethod Method { get; init; }

    /// <summary>CRAP score.</summary>
    public required double Score { get; init; }

    /// <summary>True when <see cref="Score"/> exceeds the report threshold.</summary>
    public required bool Flagged { get; init; }
}
