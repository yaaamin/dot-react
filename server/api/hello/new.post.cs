using Server.Services.AuditLogs;

namespace Server.Api;

// POST /api/hello/new
public static class PostHelloNew
{
    public record Request(string Name);

    // IAuditLogger is injected by ASP.NET automatically, just by being listed as a parameter.
    public static async Task<IResult> Handle(Request body, IAuditLogger audit)
    {
        // Example: record that someone said hello. Action, then what it happened to.
        await audit.LogAsync("hello.create", targetType: "hello", targetId: body.Name);

        return TypedResults.Ok(new { body });
    }

    // Optional: extra endpoint setup (summary, auth, caching, ...)
    public static void Configure(RouteHandlerBuilder endpoint) =>
        endpoint.WithSummary("POST /api/hello/new");
}
