using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CoverageAnalyzerTests
{
    [Test]
    public async Task Shortfall_And_Gaps_From_Document()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-doc-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.90" branch-rate="0.80" lines-covered="90" lines-valid="100" branches-covered="80" branches-valid="100" version="1.0" timestamp="0">
                  <packages>
                    <package name="Demo.A" line-rate="1" branch-rate="0.5" complexity="1">
                      <classes>
                        <class name="Demo.A.C" filename="C.cs" line-rate="1" branch-rate="0.5" complexity="1">
                          <methods />
                          <lines>
                            <line number="1" hits="1" branch="false" />
                            <line number="2" hits="1" branch="true" condition-coverage="50% (1/2)" />
                            <line number="3" hits="0" branch="false" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                    <package name="Demo.B" line-rate="1" branch-rate="1" complexity="1">
                      <classes>
                        <class name="Demo.B.C" filename="C.cs" line-rate="1" branch-rate="1" complexity="1">
                          <methods />
                          <lines>
                            <line number="1" hits="1" branch="false" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);

            var doc = CoberturaDocumentParser.Load(path);
            await Assert.That(doc.Packages.Count).IsEqualTo(2);
            var a = doc.Packages.Single(p => p.Name == "Demo.A");
            await Assert.That(a.LinesValid).IsEqualTo(3);
            await Assert.That(a.LinesCovered).IsEqualTo(2);
            await Assert.That(a.BranchGap).IsEqualTo(1);

            var shortfall = CoverageAnalyzer.Shortfall(doc.Summary, 95);
            await Assert.That(shortfall.LinesNeeded).IsEqualTo(5);
            await Assert.That(shortfall.BranchesNeeded).IsEqualTo(15);
            await Assert.That(shortfall.MeetsTarget).IsFalse();

            var below = CoverageAnalyzer.PackagesBelowTarget(doc, 95, take: 10);
            await Assert.That(below.Any(p => p.Name == "Demo.A")).IsTrue();

            var md = CoverageAnalyzer.FormatGapsMarkdown(doc, 95, take: 5);
            await Assert.That(md).Contains("Demo.A");
            await Assert.That(md).Contains("Coverage gaps");

            var (failed, message) = CoverageGate.Evaluate(doc.Summary, 95);
            await Assert.That(failed).IsTrue();
            await Assert.That(message).Contains("line coverage");

            await Assert.That(() => CoverageAssert.AtLeast(doc.Summary, 95))
                .Throws<InvalidOperationException>();

            CoverageAssert.AtLeast(doc.Summary, failBelow: -1);
            CoverageAssert.AtLeast(path, failBelow: -1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Gate_Passes_When_Above_Target()
    {
        var summary = new CoberturaSummary
        {
            LinePercent = 96,
            BranchPercent = 96,
            LinesCovered = 96,
            LinesValid = 100,
            BranchesCovered = 96,
            BranchesValid = 100,
        };
        var (failed, message) = CoverageGate.Evaluate(summary, 95);
        await Assert.That(failed).IsFalse();
        await Assert.That(message).IsNull();
        CoverageAssert.AtLeast(summary, 95);
    }
}
