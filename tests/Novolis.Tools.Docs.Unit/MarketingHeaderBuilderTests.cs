using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class MarketingHeaderBuilderTests
{
    [Test]
    public async Task Build_Includes_Marker_Block()
    {
        var meta = PlatformRepoCatalog.GetOrDefault("novolis-tools");
        var header = MarketingHeaderBuilder.Build("novolis-tools", meta);
        await Assert.That(MarketingHeaderBuilder.HasMarketingBlock(header)).IsTrue();
        await Assert.That(header).Contains(MarketingHeaderBuilder.StartMarker);
        await Assert.That(header).Contains(MarketingHeaderBuilder.EndMarker);
    }

    [Test]
    public async Task MergeIntoReadme_Replaces_Existing_Block()
    {
        var meta = PlatformRepoCatalog.GetOrDefault("novolis-math");
        var header = MarketingHeaderBuilder.Build("novolis-math", meta);
        var body = """
            <!-- novolis-marketing:start -->
            old
            <!-- novolis-marketing:end -->

            # Body
            """;
        var merged = MarketingHeaderBuilder.MergeIntoReadme(body, header);
        await Assert.That(merged).Contains(meta.Tag);
        await Assert.That(merged).DoesNotContain("old");
        await Assert.That(merged).Contains("# Body");
    }
}
