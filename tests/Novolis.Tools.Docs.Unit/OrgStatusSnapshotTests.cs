using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class OrgStatusSnapshotTests
{
    [Test]
    public async Task Latest_Picks_Highest_Dotted_Version()
    {
        var latest = OrgVersions.Latest(["2026.1.1.9", "2026.1.1.41", ""]);
        await Assert.That(latest).IsEqualTo("2026.1.1.41");
    }

    [Test]
    public async Task Markdown_Lists_Failures_And_Shipped_Releases()
    {
        var snapshot = OrgStatusSnapshotFactory.Create(
            "Novolis-Platform",
            "2026-09-30 18:00 UTC",
            2,
            [
                new OrgRepoFacts(
                    "novolis-apps",
                    0,
                    "",
                    "",
                    new OrgWorkflowFact("merge.yml", "success", "merge", "https://github.com/Novolis-Platform/novolis-apps/actions/runs/1", "2026-09-30T10:00:00Z", null),
                    new OrgWorkflowFact("release.yml", "failure", "Release", "https://github.com/Novolis-Platform/novolis-apps/actions/runs/2", "2026-09-30T12:00:00Z", "error CS1001 | boom"),
                    new OrgReleaseFact("v1.2.3", "2026-09-01T08:00:00Z", "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v1.2.3", 2)),
                new OrgRepoFacts(
                    "novolis-physics",
                    2,
                    "2026.1.1.41",
                    "2026.1.0.3",
                    new OrgWorkflowFact("merge.yml", "success", "merge", "https://github.com/Novolis-Platform/novolis-physics/actions/runs/3", "2026-09-29T10:00:00Z", null),
                    null,
                    new OrgReleaseFact("2026.1.0", "2026-08-01T08:00:00Z", "https://github.com/Novolis-Platform/novolis-physics/releases/tag/2026.1.0", 0)),
            ]);

        var markdown = OrgStatusMarkdown.Build(snapshot);
        await Assert.That(markdown).Contains("brand/status/failure.svg");
        await Assert.That(markdown).Contains("alt=\"Failed\"");
        await Assert.That(markdown).Contains("novolis-apps");
        await Assert.That(markdown).Contains("alt=\"release.yml\"");
        await Assert.That(markdown).Contains("error CS1001 \\| boom");
        await Assert.That(markdown).Contains("alt=\"Shipped\"");
        await Assert.That(markdown).Contains("`v1.2.3`");
        await Assert.That(markdown).Contains("brand/status/release.svg");
        await Assert.That(markdown).Contains("`2`");
        await Assert.That(markdown).Contains("alt=\"What we have\"");
        await Assert.That(markdown).Contains("`2026.1.1.41`");
        await Assert.That(markdown).Contains("brand/status/nuget.svg");
        await Assert.That(markdown).Contains("`2026.1.0.3`");
        await Assert.That(snapshot.FailedCount).IsEqualTo(1);
        await Assert.That(snapshot.ReleasedRepoCount).IsEqualTo(2);
        await Assert.That(snapshot.MergeSuccesses).IsEqualTo(2);

        var html = OrgStatusHtml.Bands(snapshot);
        await Assert.That(html).Contains("id=\"failed\"");
        await Assert.That(html).Contains("id=\"shipped\"");
        await Assert.That(html).Contains("v1.2.3");
        await Assert.That(html).Contains("status-mark");
        await Assert.That(html).Contains("mark-fail");
        await Assert.That(OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-physics"))).Contains("2026.1.1.41");
    }
}
