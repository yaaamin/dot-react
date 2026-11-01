namespace Server.Models.AuditLogs;

/// <summary>
/// One row in the audit_logs table: "who did what to which thing, and when".
/// </summary>
public class AuditLog
{
    public string Id { get; set; } = string.Empty;
    public string? ActorType { get; set; }
    public string? ActorId { get; set; }
    public string? Action { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
