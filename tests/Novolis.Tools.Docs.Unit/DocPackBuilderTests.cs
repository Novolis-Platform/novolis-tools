using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class DocPackBuilderTests
{
    [Test]
    public async Task Scaffold_Writes_Expected_Files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "novolis-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var written = DocPackBuilder.Scaffold("Sample").WriteTo(dir);
            await Assert.That(written.Count).IsEqualTo(4);
            await Assert.That(File.Exists(Path.Combine(dir, "overview.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "architecture.md"))).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir, "architecture.md")))
                .Contains("```mermaid");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
