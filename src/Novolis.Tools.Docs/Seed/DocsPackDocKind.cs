using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Policy doc kinds for thin-document detection.</summary>
public enum DocsPackDocKind
{
    /// <summary>docs/README.md</summary>
    Readme,

    /// <summary>docs/getting-started.md</summary>
    GettingStarted,

    /// <summary>docs/design.md</summary>
    Design,

    /// <summary>docs/release.md</summary>
    Release,
}
