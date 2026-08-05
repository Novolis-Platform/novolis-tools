using Novolis.Tools.Cli;

namespace Novolis.Tools.Cli.Unit;

public sealed class OpenGuardsTests
{
    [Test]
    public async Task Resolve_Memory_Ok()
    {
        var source = OpenGuards.ResolveDataSource(":memory:", create: false, readOnly: false, out var label);
        await Assert.That(source).IsEqualTo(":memory:");
        await Assert.That(label).IsEqualTo(":memory:");
    }

    [Test]
    public async Task Resolve_Missing_File_Requires_Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-missing-" + Guid.NewGuid().ToString("N") + ".db");
        await Assert.That(() => OpenGuards.ResolveDataSource(path, create: false, readOnly: false, out _))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task Resolve_Create_Allows_Missing()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-create-" + Guid.NewGuid().ToString("N") + ".db");
        var source = OpenGuards.ResolveDataSource(path, create: true, readOnly: false, out _);
        await Assert.That(source).IsEqualTo(Path.GetFullPath(path));
    }

    [Test]
    public async Task ResultShaping_Truncates()
    {
        var prefs = new ReplPreferences { Limit = 2 };
        var rows = new IReadOnlyList<string>[] { ["a"], ["b"], ["c"] };
        var shaped = ResultShaping.Shape(["n"], rows, null, "row", prefs, TimeSpan.FromMilliseconds(3));
        await Assert.That(shaped.Rows.Count).IsEqualTo(2);
        await Assert.That(shaped.Truncated).IsTrue();
    }
}

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
