using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class DocsPackThinDocTests
{
    [Test]
    public async Task ShouldReplace_Detects_Reserved_Stub_When_OverwriteThin()
    {
        var dir = Path.Combine(Path.GetTempPath(), "novolis-thin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "design.md");
        await File.WriteAllTextAsync(path, "# Design\n\nReserved for future content.\n");
        try
        {
            await Assert.That(DocsPackThinDoc.ShouldReplace(path, DocsPackDocKind.Design, "novolis-sample", overwriteThin: false)).IsFalse();
            await Assert.That(DocsPackThinDoc.ShouldReplace(path, DocsPackDocKind.Design, "novolis-sample", overwriteThin: true)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task ShouldReplace_Treats_Missing_File_As_Replace()
    {
        var path = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".md");
        await Assert.That(DocsPackThinDoc.ShouldReplace(path, DocsPackDocKind.Readme, "novolis-sample", overwriteThin: false)).IsTrue();
    }
}
