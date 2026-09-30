using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Counters from a docs pack seed run.</summary>
public sealed class DocsPackSeedResult
{
    /// <summary>Repositories processed.</summary>
    public int Repos { get; set; }

    /// <summary>docs/README.md writes.</summary>
    public int Readme { get; set; }

    /// <summary>getting-started.md writes.</summary>
    public int GettingStarted { get; set; }

    /// <summary>design.md writes.</summary>
    public int Design { get; set; }

    /// <summary>release.md writes.</summary>
    public int Release { get; set; }

    /// <summary>Root README marketing updates.</summary>
    public int Marketing { get; set; }
}
