using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

/// <summary>One Cobertura method with Coverlet complexity and rates.</summary>
public sealed class CoberturaMethod
{
    /// <summary>Assembly / package name.</summary>
    public required string PackageName { get; init; }

    /// <summary>Fully qualified type name from Cobertura <c>class/@name</c>.</summary>
    public required string TypeName { get; init; }

    /// <summary>Method name (e.g. <c>Parse</c>, <c>.ctor</c>, <c>get_Foo</c>).</summary>
    public required string MethodName { get; init; }

    /// <summary>Cobertura signature string (e.g. <c>(string)</c>).</summary>
    public required string Signature { get; init; }

    /// <summary>Source file path from Cobertura (may be absolute).</summary>
    public string? FileName { get; init; }

    /// <summary>Cyclomatic complexity from Coverlet (<c>method/@complexity</c>).</summary>
    public int Complexity { get; init; }

    /// <summary>Line coverage rate 0–1.</summary>
    public double LineRate { get; init; }

    /// <summary>Branch coverage rate 0–1.</summary>
    public double BranchRate { get; init; }

    /// <summary>Line coverage percent 0–100.</summary>
    public double LinePercent => Math.Round(LineRate * 100.0, 1);

    /// <summary>Branch coverage percent 0–100.</summary>
    public double BranchPercent => Math.Round(BranchRate * 100.0, 1);

    /// <summary>Display name: type + method + signature.</summary>
    public string DisplayName => $"{TypeName}.{MethodName}{Signature}";
}
