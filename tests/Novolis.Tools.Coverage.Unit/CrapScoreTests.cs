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
