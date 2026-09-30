using Novolis.Tools.Cli;

namespace Novolis.Tools.Cli.Unit;

public sealed class GuardHeuristicTests
{
    [Test]
    public async Task Resolve_ConnectionString_Still_Requires_Create()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-cs-" + Guid.NewGuid().ToString("N") + ".db");
        var cs = "Data Source=" + path;
        await Assert.That(() => OpenGuards.ResolveDataSource(cs, create: false, readOnly: false, out _))
            .Throws<FileNotFoundException>();
    }

    [Test]
    public async Task IsWriteSql_Detects_With_Delete()
    {
        await Assert.That(ReplChrome.IsWriteSql("WITH x AS (SELECT 1) DELETE FROM t")).IsTrue();
        await Assert.That(ReplChrome.IsWriteSql("WITH x AS (SELECT 1) SELECT * FROM x")).IsFalse();
        await Assert.That(ReplChrome.IsWriteSql("SELECT 1")).IsFalse();
    }
}
