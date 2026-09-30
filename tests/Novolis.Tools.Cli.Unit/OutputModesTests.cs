using Novolis.Tools.Cli;

namespace Novolis.Tools.Cli.Unit;

public sealed class OutputModesTests
{
    [Test]
    public async Task Parse_Aliases()
    {
        await Assert.That(OutputModes.TryParse("json", out var m)).IsTrue();
        await Assert.That(m).IsEqualTo(OutputMode.Json);
        await Assert.That(OutputModes.TryParse("nope", out _)).IsFalse();
    }
}
