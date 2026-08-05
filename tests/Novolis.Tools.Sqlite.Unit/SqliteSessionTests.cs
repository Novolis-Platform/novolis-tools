using Novolis.Storage.Sqlite;
using Novolis.Tools.Sqlite;

namespace Novolis.Tools.Sqlite.Unit;

public sealed class SqliteSessionTests
{
    [Test]
    public async Task Execute_Create_Select_And_ListTables()
    {
        await using var session = SqliteSession.Open(":memory:");
        await session.ExecuteAsync("CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL);");
        await session.ExecuteAsync("INSERT INTO items (name) VALUES ('alpha'), ('beta');");

        var tables = await session.ListTablesAsync();
        await Assert.That(tables).Contains("items");

        var result = await session.ExecuteAsync("SELECT id, name FROM items ORDER BY id;");
        await Assert.That(result.Rows.Count).IsEqualTo(2);
        await Assert.That(result.ToTable()).Contains("alpha");
        await Assert.That(result.ToCsv()).Contains("id,name");

        var schema = await session.GetSchemaAsync("items");
        await Assert.That(schema).Contains("CREATE TABLE");
    }

    [Test]
    public async Task Open_Accepts_Storage_SqliteOptions()
    {
        await using var session = SqliteSession.Open(new SqliteOptions
        {
            ConnectionString = "Data Source=:memory:",
        });

        var result = await session.ExecuteAsync("SELECT 7 AS n;");
        await Assert.That(result.Rows[0][0]).IsEqualTo("7");
    }

    [Test]
    public async Task ListTableInfos_Includes_Counts()
    {
        await using var session = SqliteSession.Open(":memory:");
        await session.ExecuteAsync("CREATE TABLE items (id INTEGER PRIMARY KEY);");
        await session.ExecuteAsync("INSERT INTO items DEFAULT VALUES;");
        await session.ExecuteAsync("INSERT INTO items DEFAULT VALUES;");

        var infos = await session.ListTableInfosAsync();
        await Assert.That(infos.Count).IsEqualTo(1);
        await Assert.That(infos[0].Name).IsEqualTo("items");
        await Assert.That(infos[0].RowCount).IsEqualTo(2);
    }

    [Test]
    public async Task Open_ReadOnly_Rejects_Writes()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-sqlite-ro-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (var setup = SqliteSession.Open(path))
            {
                await setup.ExecuteAsync("CREATE TABLE t (id INTEGER);");
            }

            await using var session = SqliteSession.Open(path, readOnly: true);
            await Assert.That(session.IsReadOnly).IsTrue();
            await Assert.That(async () => await session.ExecuteAsync("INSERT INTO t VALUES (1);"))
                .Throws<Exception>();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
