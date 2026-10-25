namespace Server.Api;

// POST /api/hello/new
public static class PostHelloNew
{
    public record Request(string Name);

    public static IResult Handle(Request body) =>
        TypedResults.Ok(new { body });

    // Optional: extra endpoint setup (summary, auth, caching, ...)
    public static void Configure(RouteHandlerBuilder endpoint) =>
        endpoint.WithSummary("POST /api/hello/new");
}
