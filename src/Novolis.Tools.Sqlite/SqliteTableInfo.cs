using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Sqlite;
using MsSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Novolis.Tools.Sqlite;

/// <summary>Table name + row count (-1 when count failed).</summary>
/// <param name="Name">Table name.</param>
/// <param name="RowCount">Approximate COUNT(*).</param>
public sealed record SqliteTableInfo(string Name, long RowCount);
