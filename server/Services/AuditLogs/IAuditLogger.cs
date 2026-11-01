namespace Server.Services.AuditLogs;

/// <summary>
/// The "contract" for writing audit logs. Endpoints depend on this interface and never on the
/// concrete class, so the real database writer can be swapped for a fake one in tests.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Write one audit log row.</summary>
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Shortcut for the common case: just say what happened and to what.</summary>
    Task LogAsync(string action, string? targetType = null, string? targetId = null,
        CancellationToken cancellationToken = default);
}
