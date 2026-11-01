using System.Data.Common;
using MySqlConnector;

namespace Server.Data;

/// <summary>The MySQL version of <see cref="IDbConnectionFactory"/>.</summary>
public sealed class MySqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            // Opening failed, so nobody else will ever dispose this connection. Clean it up here.
            await connection.DisposeAsync();
            throw;
        }
    }
}
