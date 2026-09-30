using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class PlatformRepoCatalogTests
{
    [Test]
    public async Task Entries_Contains_Known_Repo()
    {
        await Assert.That(PlatformRepoCatalog.Entries.ContainsKey("novolis-documents")).IsTrue();
        var meta = PlatformRepoCatalog.GetOrDefault("novolis-documents");
        await Assert.That(meta.Tag).Contains("Skia PDF");
    }

    [Test]
    public async Task GetOrDefault_Unknown_Repo_Uses_Fallback()
    {
        var meta = PlatformRepoCatalog.GetOrDefault("novolis-unknown-repo");
        await Assert.That(meta.Blurb).Contains("novolis-unknown-repo");
    }
}
