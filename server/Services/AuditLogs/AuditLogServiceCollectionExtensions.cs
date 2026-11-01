using Server.Data;

namespace Server.Services.AuditLogs;

public static class AuditLogServiceCollectionExtensions
{
    /// <summary>
    /// One call in Program.cs that wires up everything the audit logger needs:
    /// the database connection maker and the logger itself.
    /// </summary>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Missing connection string 'ConnectionStrings:Default' (see appsettings.json).");

        services.AddHttpContextAccessor();                                      // lets the logger see the current request/user
        services.AddSingleton<IDbConnectionFactory>(new MySqlConnectionFactory(connectionString));
        services.AddScoped<IAuditLogger, AuditLogger>();                        // one per request
        return services;
    }
}
