using System.Data;
using System.Security.Claims;
using Dapper;
using Server.Data;
using Server.Models.AuditLogs;

namespace Server.Services.AuditLogs;

/// <summary>
/// The real <see cref="IAuditLogger"/>: fills in the blanks and saves the row through the
/// sp_audit_log_create stored procedure.
/// </summary>
// `internal` = invisible outside this project. Combined with living outside server/api/,
// there is no way for an outside caller to reach it except through code inside the app.
internal sealed class AuditLogger(
    IDbConnectionFactory connectionFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    const string CorrelationHeader = "X-Correlation-Id";

    public Task LogAsync(string action, string? targetType = null, string? targetId = null,
        CancellationToken cancellationToken = default) =>
        LogAsync(new AuditEntry(action, targetType, targetId), cancellationToken);

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var row = Build(entry);

            await using var connection = await connectionFactory.OpenAsync(cancellationToken);

            // Dapper calls the stored procedure; each p_* name matches a parameter of the procedure.
            await connection.ExecuteAsync(new CommandDefinition(
                "sp_audit_log_create",
                new
                {
                    p_id = row.Id,
                    p_actor_type = row.ActorType,
                    p_actor_id = row.ActorId,
                    p_action = row.Action,
                    p_correlation_id = row.CorrelationId,
                    p_occurred_at = row.OccurredAt,
                    p_target_type = row.TargetType,
                    p_target_id = row.TargetId,
                    p_created_at = row.CreatedAt,
                    p_created_by = row.CreatedBy,
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw; // the request was cancelled on purpose; that is not a logging failure
        }
        catch (Exception ex)
        {
            // A broken audit log should not crash the real request (the user's action already
            // happened). We record the problem in the normal app log instead.
            // If you need "no audit row = the action must fail", delete this catch block.
            logger.LogError(ex, "Failed to write audit log for action {Action}", entry.Action);
        }
    }

    /// <summary>Turns what the caller gave us into a complete row, filling in anything missing.</summary>
    AuditLog Build(AuditEntry entry)
    {
        var http = httpContextAccessor.HttpContext;
        var now = DateTime.UtcNow;

        // Who did it? Use what the caller said; otherwise the signed-in user; otherwise "system".
        var userId = http?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var actorId = entry.ActorId ?? userId;
        var actorType = entry.ActorType ?? (userId is not null ? "user" : "system");

        // Which request is this part of? Use the caller's value, then the request header, then
        // ASP.NET's own per-request id (empty when we are not inside a web request).
        var correlationId = entry.CorrelationId
            ?? http?.Request.Headers[CorrelationHeader].FirstOrDefault()
            ?? http?.TraceIdentifier;

        return new AuditLog
        {
            Id = Guid.NewGuid().ToString(),
            ActorType = actorType,
            ActorId = actorId,
            Action = entry.Action,
            CorrelationId = correlationId,
            OccurredAt = now,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            CreatedAt = now,
            CreatedBy = actorId ?? actorType,
        };
    }
}
