using Novolis.Storage.LiteDb;
using Novolis.Tools.LiteDb;

namespace Novolis.Tools.LiteDb.Unit;

public sealed class LiteDbSessionTests
{
    [Test]
    public async Task Execute_Insert_Select_And_ListCollections()
    {
        using var session = LiteDbSession.Open(":memory:");
        session.Execute("INSERT INTO items VALUES {_id: 1, name: \"alpha\"}");
        session.Execute("INSERT INTO items VALUES {_id: 2, name: \"beta\"}");

        var collections = session.ListCollections();
        await Assert.That(collections).Contains("items");

        var result = session.Execute("SELECT $ FROM items ORDER BY _id");
        await Assert.That(result.Rows.Count).IsEqualTo(2);
        await Assert.That(result.ToTable()).Contains("alpha");
        await Assert.That(result.ToCsv()).Contains("name");
        await Assert.That(result.ToJsonLines()).Contains("beta");

        var indexes = session.GetIndexes("items");
        await Assert.That(indexes).Contains("items");
    }

    [Test]
    public async Task Open_Accepts_Storage_LiteDbOptions()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-tools-litedb-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var session = LiteDbSession.Open(new LiteDbOptions { DatabasePath = path }))
            {
                session.Execute("INSERT INTO notes VALUES {_id: 1, body: \"hello\"}");
            }

            using var reopen = LiteDbSession.Open(new LiteDbOptions { DatabasePath = path });
            var result = reopen.Execute("SELECT $ FROM notes");
            await Assert.That(result.Rows.Count).IsEqualTo(1);
            await Assert.That(result.ToTable()).Contains("hello");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task BuildConnectionString_Matches_Storage_Conventions()
    {
        var shared = LiteDbSession.BuildConnectionString("app.db", password: null);
        await Assert.That(shared).IsEqualTo("Filename=app.db;Connection=shared");

        var memory = LiteDbSession.BuildConnectionString(":memory:", password: null);
        await Assert.That(memory).IsEqualTo("Filename=:memory:");

        var locked = LiteDbSession.BuildConnectionString("secret.db", "pw");
        await Assert.That(locked).Contains("Password=pw");
        await Assert.That(locked).Contains("Connection=shared");

        var ro = LiteDbSession.BuildConnectionString("app.db", null, readOnly: true);
        await Assert.That(ro).Contains("ReadOnly=true");
    }

    [Test]
    public async Task ListCollectionInfos_Includes_Counts()
    {
        using var session = LiteDbSession.Open(":memory:");
        session.Execute("INSERT INTO items VALUES {_id: 1, name: \"a\"}");
        session.Execute("INSERT INTO items VALUES {_id: 2, name: \"b\"}");
        var infos = session.ListCollectionInfos();
        await Assert.That(infos.Count).IsEqualTo(1);
        await Assert.That(infos[0].DocumentCount).IsEqualTo(2);
    }
}
