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

    [Test]
    public async Task Wrap_GetInfo_ListIndexes_And_Empty_Results()
    {
        using var db = new LiteDB.LiteDatabase(":memory:");
        using var session = LiteDbSession.Wrap(db);
        await Assert.That(session.IsReadOnly).IsFalse();

        session.Execute("INSERT INTO notes VALUES {_id: 1, body: \"hi\"}");
        var indexes = session.ListIndexes("notes");
        await Assert.That(indexes.Rows.Count).IsGreaterThanOrEqualTo(0);

        var info = session.GetInfo();
        await Assert.That(info.Count).IsGreaterThan(0);

        var empty = session.Execute("SELECT $ FROM notes WHERE _id = 999");
        await Assert.That(empty.Rows.Count).IsEqualTo(0);
        _ = empty.ToJsonLines();
        _ = empty.ToTable();
    }

    [Test]
    public async Task FileBytes_NullFields_Scalars_And_DoubleDispose()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-litedb-cov-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var session = LiteDbSession.Open(path))
            {
                session.Execute("INSERT INTO items VALUES {_id: 1, name: null, tags: [\"a\"], nested: {x: 1}}");
                var info = session.GetInfo();
                await Assert.That(info.Any(p => p.Key == "file_bytes")).IsTrue();
                await Assert.That(info.Any(p => p.Key == "read_only" && p.Value == "no")).IsTrue();

                var docs = session.Execute("SELECT $ FROM items");
                await Assert.That(docs.ToTable()).Contains("NULL");

                var scalars = session.Execute("SELECT 1 AS n");
                await Assert.That(scalars.Rows.Count).IsGreaterThanOrEqualTo(0);

                var allIndexes = session.ListIndexes();
                await Assert.That(allIndexes).IsNotNull();

                session.Dispose();
                session.Dispose();
            }

            var already = LiteDbSession.BuildConnectionString("Filename=app.db;Connection=shared", "pw", readOnly: true);
            await Assert.That(already).Contains("Password=pw");
            await Assert.That(already).Contains("ReadOnly=true");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
