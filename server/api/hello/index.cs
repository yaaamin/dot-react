namespace Server.Api;

// GET /api/hello
public static class GetHello
{
    public static IResult Handle() =>
        TypedResults.Ok(new { message = "Hello from GET /api/hello" });

    // Optional: extra endpoint setup (summary, auth, caching, ...)
    public static void Configure(RouteHandlerBuilder endpoint) =>
        endpoint.WithSummary("GET /api/hello");
        
}
