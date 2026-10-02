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
    public async Task Markdown_Lists_Merge_Failures_And_Shipped_Releases()
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
                    "novolis-audio",
                    4,
                    "2026.1.10.84",
                    "",
                    new OrgWorkflowFact("merge.yml", "success", "merge", "https://github.com/Novolis-Platform/novolis-audio/actions/runs/5", "2026-09-30T09:00:00Z", null),
                    new OrgWorkflowFact("release.yml", "failure", "v2026.1.10", "https://github.com/Novolis-Platform/novolis-audio/actions/runs/6", "2026-07-28T19:30:00Z", "Secret NUGET_API_KEY is not set"),
                    new OrgReleaseFact("v2026.1.10", "2026-07-28T19:30:00Z", "https://github.com/Novolis-Platform/novolis-audio/releases/tag/v2026.1.10", 0)),
                new OrgRepoFacts(
                    "novolis-lab",
                    0,
                    "",
                    "",
                    new OrgWorkflowFact("merge.yml", "failure", "merge", "https://github.com/Novolis-Platform/novolis-lab/actions/runs/4", "2026-09-30T11:00:00Z", "error CS0246 | boom"),
                    null,
                    null),
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
        await Assert.That(markdown).Contains("Failed");
        await Assert.That(markdown).Contains("novolis-lab");
        await Assert.That(markdown).Contains("[failed](");
        await Assert.That(markdown).Contains("error CS0246 | boom");
        await Assert.That(markdown).Contains("error CS1001 | boom");
        await Assert.That(markdown).DoesNotContain("NUGET_API_KEY");
        await Assert.That(snapshot.Failures.Select(row => row.Repo).ToArray()).IsEquivalentTo(new[] { "novolis-apps", "novolis-lab" });
        await Assert.That(snapshot.Releases.Select(row => row.Repo).ToArray()).IsEquivalentTo(new[] { "novolis-apps" });
        await Assert.That(markdown).Contains("Shipped");
        await Assert.That(markdown).Contains("`v1.2.3`");
        await Assert.That(markdown).Contains("2 assets");
        await Assert.That(markdown).Contains("What we have");
        await Assert.That(markdown).Contains("`2026.1.1.41`");
        await Assert.That(markdown).Contains("nuget.org");
        await Assert.That(markdown).Contains("`2026.1.0.3`");
        await Assert.That(snapshot.FailedCount).IsEqualTo(2);
        await Assert.That(snapshot.ReleasedRepoCount).IsEqualTo(1);
        await Assert.That(snapshot.MergeSuccesses).IsEqualTo(3);

        var html = OrgStatusHtml.Bands(snapshot);
        await Assert.That(html).Contains("id=\"failed\"");
        await Assert.That(html).Contains("data-repo=\"novolis-lab\"");
        await Assert.That(html).Contains("data-workflow=\"merge.yml\"");
        await Assert.That(html).Contains("id=\"status-stamp\"");
        await Assert.That(html).Contains("id=\"shipped\"");
        await Assert.That(html).Contains("v1.2.3");
        await Assert.That(html).Contains(">Failed</span>");
        await Assert.That(html).Contains(">Shipped</span>");
        await Assert.That(html).Contains(">failed</span>");
        await Assert.That(html).Contains("2 assets");
        var physics = OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-physics"), "Novolis-Platform");
        await Assert.That(physics).Contains("Packages");
        await Assert.That(physics).Contains("2026.1.1.41");
        await Assert.That(physics).Contains("2026.1.0.3");
        await Assert.That(physics).Contains("https://github.com/Novolis-Platform/novolis-physics/releases/tag/2026.1.0");
        await Assert.That(physics).Contains("packages?repo_name=novolis-physics");
        await Assert.That(OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-audio"))).Contains("missing");
        await Assert.That(OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-apps"))).DoesNotContain("nuget.org");
        await Assert.That(OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-apps"))).Contains("Release failed");
        await Assert.That(OrgStatusHtml.CardFacts(snapshot.Repos.First(r => r.Name == "novolis-audio"))).DoesNotContain("Release failed");
        await Assert.That(markdown).Contains("release [failed](");
        await Assert.That(markdown).DoesNotContain("actions/runs/6");
    }

    [Test]
    public async Task Superseded_Merge_Cancel_Is_Not_A_Failure()
    {
        var snapshot = OrgStatusSnapshotFactory.Create(
            "Novolis-Platform",
            "2026-09-30 18:00 UTC",
            0,
            [
                new OrgRepoFacts(
                    "novolis-tools",
                    1,
                    "2026.1.1.1",
                    "",
                    new OrgWorkflowFact(
                        "merge.yml",
                        "cancelled",
                        "merge",
                        "https://github.com/Novolis-Platform/novolis-tools/actions/runs/9",
                        "2026-09-30T18:44:00Z",
                        "Canceling since a higher priority waiting request for merge.yml exists"),
                    null,
                    null),
            ]);

        await Assert.That(snapshot.FailedCount).IsEqualTo(0);
        await Assert.That(OrgFailureText.Prefer(
            "Process completed with exit code 1",
            "error CS0246: The type or namespace name 'Novolis' could not be found")).Contains("CS0246");
    }

    [Test]
    public async Task Library_Tag_Without_Assets_Is_Not_Shipped()
    {
        var snapshot = OrgStatusSnapshotFactory.Create(
            "Novolis-Platform",
            "2026-09-30 18:00 UTC",
            4,
            [
                new OrgRepoFacts(
                    "novolis-avalonia",
                    40,
                    "2026.1.6.189",
                    "",
                    new OrgWorkflowFact("merge.yml", "success", "merge", "https://github.com/Novolis-Platform/novolis-avalonia/actions/runs/7", "2026-09-30T11:00:00Z", null),
                    new OrgWorkflowFact("release.yml", "failure", "0.1", "https://github.com/Novolis-Platform/novolis-avalonia/actions/runs/8", "2026-06-06T21:55:00Z", "Secret NUGET_API_KEY is not set"),
                    new OrgReleaseFact("0.1", "2026-06-06T21:55:00Z", "https://github.com/Novolis-Platform/novolis-avalonia/releases/tag/0.1", 0)),
            ]);

        await Assert.That(snapshot.ReleasedRepoCount).IsEqualTo(0);
        await Assert.That(snapshot.FailedCount).IsEqualTo(0);
        await Assert.That(snapshot.Repos[0].GprVersion).IsEqualTo("2026.1.6.189");
        await Assert.That(snapshot.Repos[0].ReleaseTag).IsEqualTo("0.1");
    }

    [Test]
    public async Task App_Downloads_Group_Installers_And_Link_The_Release()
    {
        var apk = "https://github.com/Novolis-Platform/novolis-apps/releases/download/v2026.1.0.45/BooksMobile-2026.1.0.45-android.apk";
        var exe = "https://github.com/Novolis-Platform/novolis-apps/releases/download/v2026.1.0.45/BooksMobileSetup-2026.1.0.45-win-x64.exe";
        var sums = "https://github.com/Novolis-Platform/novolis-apps/releases/download/v2026.1.0.45/SHA256SUMS.txt";
        var snapshot = OrgStatusSnapshotFactory.Create(
            "Novolis-Platform",
            "2026-10-02 05:00 UTC",
            0,
            [
                new OrgRepoFacts(
                    "novolis-apps",
                    0,
                    "",
                    "",
                    null,
                    null,
                    new OrgReleaseFact(
                        "v2026.1.0.45",
                        "2026-09-30T18:00:00Z",
                        "https://github.com/Novolis-Platform/novolis-apps/releases/tag/v2026.1.0.45",
                        3,
                        [
                            new OrgReleaseAsset("BooksMobile-2026.1.0.45-android.apk", 53_394_079, apk),
                            new OrgReleaseAsset("BooksMobileSetup-2026.1.0.45-win-x64.exe", 70_269_770, exe),
                            new OrgReleaseAsset("CadStudio3DSetup-2026.1.0.45-win-x64.exe", 58_381_994, "https://example.test/cad.exe"),
                            new OrgReleaseAsset("SHA256SUMS.txt", 2013, sums),
                        ])),
            ]);

        await Assert.That(OrgDownloadCatalog.DisplayName("CadStudio3D")).IsEqualTo("Cad Studio 3D");
        await Assert.That(OrgDownloadCatalog.DisplayName("NovolisPdfReader")).IsEqualTo("Novolis Pdf Reader");
        var groups = OrgDownloadCatalog.From(snapshot.Releases);
        await Assert.That(groups).Count().IsEqualTo(1);
        await Assert.That(groups[0].Apps.Select(app => app.Name).ToArray()).IsEquivalentTo(new[] { "Books Mobile", "Cad Studio 3D" });
        await Assert.That(groups[0].Apps[0].Android).Count().IsEqualTo(1);
        await Assert.That(groups[0].Apps[0].Windows).Count().IsEqualTo(1);
        await Assert.That(groups[0].Checksums).Count().IsEqualTo(1);

        var html = OrgStatusHtml.Bands(snapshot);
        await Assert.That(html).Contains("id=\"downloads\"");
        await Assert.That(html).Contains("Latest app downloads");
        await Assert.That(html).Contains(apk);
        await Assert.That(html).Contains("Books Mobile");
        await Assert.That(html).Contains("SHA256SUMS.txt");
        await Assert.That(html).Contains("href=\"#downloads\"");

        var markdown = OrgStatusMarkdown.Build(snapshot);
        await Assert.That(markdown).Contains("### Latest app downloads");
        await Assert.That(markdown).Contains("[Android 50.9 MB](" + apk + ")");
        await Assert.That(markdown).Contains("[Windows 67 MB](" + exe + ")");
        await Assert.That(markdown).Contains("SHA256SUMS.txt");
    }
}
