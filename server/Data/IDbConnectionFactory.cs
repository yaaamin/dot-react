using System.Data.Common;

namespace Server.Data;

/// <summary>
/// A "connection maker". Anything that needs the database asks this for a fresh connection
/// instead of building one itself, so the connection string lives in exactly one place.
/// </summary>
public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default);
}
