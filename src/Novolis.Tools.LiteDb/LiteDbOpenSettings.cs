using System.Text;
using LiteDB;
using Novolis.Storage.LiteDb;

namespace Novolis.Tools.LiteDb;

/// <summary>Open flags for <see cref="LiteDbSession"/>.</summary>
/// <param name="DataSource">File path, <c>:memory:</c>, or connection string (caller-resolved path preferred).</param>
/// <param name="Password">Optional AES password.</param>
/// <param name="ReadOnly">Open existing file read-only.</param>
public sealed record LiteDbOpenSettings(string DataSource, string? Password = null, bool ReadOnly = false);
