using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

/// <summary>Parsed Cobertura document (root + packages).</summary>
public sealed class CoberturaDocument
{
    /// <summary>Root summary rates.</summary>
    public required CoberturaSummary Summary { get; init; }

    /// <summary>Per-package rollups.</summary>
    public required IReadOnlyList<CoberturaPackage> Packages { get; init; }

    /// <summary>Per-method rows (Coverlet includes complexity + rates).</summary>
    public IReadOnlyList<CoberturaMethod> Methods { get; init; } = [];

    /// <summary>Source path when loaded from disk.</summary>
    public string? SourcePath { get; init; }
}
