using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CoberturaSummaryParserTests
{
    [Test]
    public async Task Parse_Reads_Rates()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.936" branch-rate="0.724" lines-covered="100" lines-valid="107" branches-covered="50" branches-valid="69" version="1.0" timestamp="0">
                </coverage>
                """);
            var sum = CoberturaSummaryParser.Parse(path);
            await Assert.That(sum.LinePercent).IsEqualTo(93.6);
            await Assert.That(sum.BranchPercent).IsEqualTo(72.4);
            await Assert.That(sum.LinesCovered).IsEqualTo(100);
            await Assert.That(sum.LinesValid).IsEqualTo(107);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
