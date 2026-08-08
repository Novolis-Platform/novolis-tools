using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CrapScoreTests
{
    [Test]
    public async Task Compute_FullCoverage_Equals_Complexity()
    {
        await Assert.That(CrapScore.Compute(12, 1.0)).IsEqualTo(12.0);
        await Assert.That(CrapScore.Compute(1, 1.0)).IsEqualTo(1.0);
    }

    [Test]
    public async Task Compute_ZeroCoverage_Is_CcSquared_Plus_Cc()
    {
        await Assert.That(CrapScore.Compute(6, 0)).IsEqualTo(42.0);
        await Assert.That(CrapScore.Compute(1, 0)).IsEqualTo(2.0);
    }

    [Test]
    public async Task Compute_Clamps_Inputs()
    {
        await Assert.That(CrapScore.Compute(0, 0.5)).IsEqualTo(CrapScore.Compute(1, 0.5));
        await Assert.That(CrapScore.Compute(5, -1)).IsEqualTo(CrapScore.Compute(5, 0));
        await Assert.That(CrapScore.Compute(5, 2)).IsEqualTo(CrapScore.Compute(5, 1));
    }
}

public sealed class CrapAnalyzerTests
{
    [Test]
    public async Task Analyze_Scores_And_Flags_From_Cobertura_Methods()
    {
        var path = Path.Combine(Path.GetTempPath(), "crap-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.5" branch-rate="0.5" lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0" version="1.0" timestamp="0">
                  <packages>
                    <package name="Demo" line-rate="0.5" branch-rate="1" complexity="7">
                      <classes>
                        <class name="Demo.Foo" filename="Foo.cs" line-rate="0.5" branch-rate="1" complexity="7">
                          <methods>
                            <method name="Safe" signature="()" line-rate="1" branch-rate="1" complexity="2">
                              <lines><line number="1" hits="1" branch="false" /></lines>
                            </method>
                            <method name="Risky" signature="(int)" line-rate="0" branch-rate="0" complexity="6">
                              <lines><line number="10" hits="0" branch="false" /></lines>
                            </method>
                            <method name=".cctor" signature="()" line-rate="0" branch-rate="0" complexity="50">
                              <lines><line number="99" hits="0" branch="false" /></lines>
                            </method>
                          </methods>
                          <lines>
                            <line number="1" hits="1" branch="false" />
                            <line number="10" hits="0" branch="false" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);

            var doc = CoberturaDocumentParser.Load(path);
            await Assert.That(doc.Methods.Count).IsEqualTo(3);

            var report = CrapAnalyzer.Analyze(doc, threshold: 30);
            await Assert.That(report.Methods.Count).IsEqualTo(2); // .cctor skipped
            await Assert.That(report.Methods[0].Method.MethodName).IsEqualTo("Risky");
            await Assert.That(report.Methods[0].Score).IsEqualTo(42.0);
            await Assert.That(report.Methods[0].Flagged).IsTrue();
            await Assert.That(report.FlaggedCount).IsEqualTo(1);
            await Assert.That(report.Methods[1].Method.MethodName).IsEqualTo("Safe");
            await Assert.That(report.Methods[1].Flagged).IsFalse();

            var md = CrapAnalyzer.FormatMarkdown(report, flaggedOnly: true);
            await Assert.That(md).Contains("Risky");
            await Assert.That(md).DoesNotContain("Safe");

            var outDir = Path.Combine(Path.GetTempPath(), "crap-out-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);
            try
            {
                var written = CrapAnalyzer.WriteReport(md, cwd: outDir);
                await Assert.That(written).IsEqualTo(Path.Combine(outDir, "CRAP.md"));
                await Assert.That(File.Exists(written)).IsTrue();

                var custom = CrapAnalyzer.ResolveReportPath(Path.Combine(outDir, "risk.md"));
                await Assert.That(Path.GetFileName(custom)).IsEqualTo("risk.md");

                var dirTarget = CrapAnalyzer.ResolveReportPath(outDir + Path.DirectorySeparatorChar);
                await Assert.That(dirTarget).IsEqualTo(Path.Combine(outDir, "CRAP.md"));
            }
            finally
            {
                Directory.Delete(outDir, recursive: true);
            }

            var (failed, message) = CrapAnalyzer.EvaluateGate(report, failAbove: 30);
            await Assert.That(failed).IsTrue();
            await Assert.That(message).IsNotNull();

            var (ok, _) = CrapAnalyzer.EvaluateGate(report, failAbove: -1);
            await Assert.That(ok).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task AnalyzeFiles_Parallel_Merges_Into_One_Ranking()
    {
        var dir = Path.Combine(Path.GetTempPath(), "crap-par-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a.xml");
            var b = Path.Combine(dir, "b.xml");
            await File.WriteAllTextAsync(a, MiniCobertura("PkgA", "A.T", "Ok", lineRate: "1", complexity: "2"));
            await File.WriteAllTextAsync(b, MiniCobertura("PkgB", "B.T", "Bad", lineRate: "0", complexity: "6"));

            var report = CrapAnalyzer.AnalyzeFiles([a, b], threshold: 30, maxDegreeOfParallelism: 2);
            await Assert.That(report.SourcePaths.Count).IsEqualTo(2);
            await Assert.That(report.DegreeOfParallelism).IsEqualTo(2);
            await Assert.That(report.Methods.Count).IsEqualTo(2);
            await Assert.That(report.Methods[0].Method.MethodName).IsEqualTo("Bad");
            await Assert.That(report.FlaggedCount).IsEqualTo(1);

            var md = CrapAnalyzer.FormatMarkdown(report, flaggedOnly: true);
            await Assert.That(md).Contains("2** Cobertura");
            await Assert.That(md).Contains("Bad");
            await Assert.That(md).DoesNotContain("| Ok");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string MiniCobertura(
        string package,
        string type,
        string method,
        string lineRate,
        string complexity) =>
        $"""
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.5" branch-rate="1" lines-covered="1" lines-valid="1" branches-covered="0" branches-valid="0" version="1.0" timestamp="0">
          <packages>
            <package name="{package}" line-rate="{lineRate}" branch-rate="1" complexity="{complexity}">
              <classes>
                <class name="{type}" filename="{method}.cs" line-rate="{lineRate}" branch-rate="1" complexity="{complexity}">
                  <methods>
                    <method name="{method}" signature="()" line-rate="{lineRate}" branch-rate="1" complexity="{complexity}">
                      <lines><line number="1" hits="1" branch="false" /></lines>
                    </method>
                  </methods>
                  <lines><line number="1" hits="1" branch="false" /></lines>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
