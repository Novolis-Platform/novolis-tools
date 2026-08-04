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
}
