using Novolis.Tools.MarkdownPdf;
using TUnit.Core;

namespace Novolis.Tools.MarkdownPdf.Unit;

public sealed class MarkdownPdfTests
{
    [Test]
    public async Task Themes_lists_known_ids()
    {
        var ids = MarkdownPdfThemes.All.Select(t => t.Id).ToArray();
        await Assert.That(ids).Contains("trade");
        await Assert.That(ids).Contains("report");
        await Assert.That(ids).Contains("compact");
        await Assert.That(ids).Contains("plain");
        await Assert.That(MarkdownPdfThemes.Get("TRADE").Id).IsEqualTo("trade");
    }

    [Test]
    public async Task ConvertToBytes_trade_theme_writes_pdf()
    {
        var md = """
            # Chapter 1 - Lunch Break
            > [!date] 2495.220
            > [!time] 12:30
            > [!system] K21408
            > [!location] Duckville Station

            James's lunch was spaghetti.
            """;

        var bytes = MarkdownPdfConverter.ConvertToBytes(md, "trade", new MarkdownPdfConvertRequest
        {
            Title = "Calypso",
            Author = "Novolis",
        });

        await Assert.That(bytes.Length).IsGreaterThan(800);
        await Assert.That(bytes[0]).IsEqualTo((byte)'%');
        await Assert.That(bytes[1]).IsEqualTo((byte)'P');
    }
}
