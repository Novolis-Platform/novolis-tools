using System.Diagnostics;
using System.Text.RegularExpressions;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Result counters from a marketing upgrade run.</summary>
public sealed class RepoMarketingResult
{
    /// <summary>Repositories processed.</summary>
    public int Repos { get; set; }

    /// <summary>Package README touches.</summary>
    public int PackageReadmes { get; set; }

    /// <summary>Package index sync successes.</summary>
    public int Indexes { get; set; }

    /// <summary>GitHub meta updates.</summary>
    public int Meta { get; set; }
}
