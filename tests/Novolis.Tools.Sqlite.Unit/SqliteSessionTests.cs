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

    [Test]
    public async Task ConnectionString_Open_Indexes_Schema_Info_And_CsvEscape()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-sqlite-cov-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var session = SqliteSession.Open($"Data Source={path}"))
            {
                await session.ExecuteAsync("CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT);");
                await session.ExecuteAsync("CREATE INDEX ix_items_name ON items(name);");
                await session.ExecuteAsync("INSERT INTO items (name) VALUES ('a,b'), ('\"q\"');");

                var allIndexes = await session.ListIndexesAsync();
                await Assert.That(allIndexes.Rows.Count).IsGreaterThanOrEqualTo(1);
                var byTable = await session.ListIndexesAsync("items");
                await Assert.That(byTable.Rows.Count).IsGreaterThanOrEqualTo(1);

                var fullSchema = await session.GetSchemaAsync();
                await Assert.That(fullSchema).Contains("CREATE TABLE");

                var info = await session.GetInfoAsync();
                await Assert.That(info.Count).IsGreaterThan(0);
                await Assert.That(info.Any(p => p.Key.Contains("version", StringComparison.OrdinalIgnoreCase)
                                                || p.Key.Contains("sqlite", StringComparison.OrdinalIgnoreCase)
                                                || p.Value.Length > 0)).IsTrue();

                var result = await session.ExecuteAsync("SELECT id, name FROM items ORDER BY id;");
                var csv = result.ToCsv();
                await Assert.That(csv).Contains("\"a,b\"");
                await Assert.That(csv).Contains("\"\"\"q\"\"\"");

                var affected = await session.ExecuteAsync("UPDATE items SET name = 'x' WHERE id = 1;");
                await Assert.That(affected.ToTable()).Contains("affected");
            }

            // Sync Dispose path (non-await using).
            var sync = SqliteSession.Open(path, readOnly: true);
            sync.Dispose();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public async Task EmptyColumns_ToTable_Uses_RecordsAffected()
    {
        await using var session = SqliteSession.Open(":memory:");
        var result = await session.ExecuteAsync("CREATE TABLE t (id INTEGER);");
        await Assert.That(result.Columns.Count).IsEqualTo(0);
        await Assert.That(result.ToTable()).Contains("affected");
    }

    [Test]
    public async Task ConnectionString_ReadOnly_DoubleDispose_And_NullCells()
    {
        var path = Path.Combine(Path.GetTempPath(), "novolis-sqlite-cov2-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (var setup = SqliteSession.Open(path))
            {
                await setup.ExecuteAsync("CREATE TABLE t (id INTEGER, name TEXT);");
                await setup.ExecuteAsync("INSERT INTO t (id, name) VALUES (1, NULL);");
            }

            await using var session = SqliteSession.Open($"Data Source={path}", readOnly: true);
            await Assert.That(session.IsReadOnly).IsTrue();
            var info = await session.GetInfoAsync();
            await Assert.That(info.Any(p => p.Key == "read_only" && p.Value == "yes")).IsTrue();
            await Assert.That(info.Any(p => p.Key == "file_bytes")).IsTrue();

            var result = await session.ExecuteAsync("SELECT id, name FROM t;");
            await Assert.That(result.Rows[0][1]).IsEqualTo("NULL");

            session.Dispose();
            await session.DisposeAsync();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
