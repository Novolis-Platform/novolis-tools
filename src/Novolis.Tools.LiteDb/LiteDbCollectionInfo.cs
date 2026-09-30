using System.Text;
using LiteDB;
using Novolis.Storage.LiteDb;

namespace Novolis.Tools.LiteDb;

/// <summary>Collection name + document count.</summary>
/// <param name="Name">Collection name.</param>
/// <param name="DocumentCount">Count (-1 on failure).</param>
public sealed record LiteDbCollectionInfo(string Name, long DocumentCount);
