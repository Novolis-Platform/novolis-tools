using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Sqlite;
using MsSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Novolis.Tools.Sqlite;

/// <summary>Open flags for <see cref="SqliteSession"/>.</summary>
/// <param name="DataSource">File path or <c>:memory:</c> (already resolved by the caller).</param>
/// <param name="ReadOnly">Open existing file read-only (never creates).</param>
public sealed record SqliteOpenSettings(string DataSource, bool ReadOnly = false);
