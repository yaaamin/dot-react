namespace Server.Services.AuditLogs;

/// <summary>
/// What a caller hands to the audit logger: just the facts about what happened.
/// Only <see cref="Action"/> is required. The logger fills in everything else it can
/// (id, timestamps, who the current user is, the correlation id).
/// </summary>
/// <param name="Action">What happened, e.g. "user.login" or "invoice.delete".</param>
/// <param name="TargetType">The kind of thing it happened to, e.g. "invoice".</param>
/// <param name="TargetId">Which one, e.g. "inv_123".</param>
/// <param name="ActorType">Who did it: "user", "system", "service"... Auto-detected if left null.</param>
/// <param name="ActorId">The id of whoever did it. Auto-detected from the signed-in user if left null.</param>
/// <param name="CorrelationId">Ties several logs from one request together. Auto-detected if left null.</param>
public sealed record AuditEntry(
    string Action,
    string? TargetType = null,
    string? TargetId = null,
    string? ActorType = null,
    string? ActorId = null,
    string? CorrelationId = null);
