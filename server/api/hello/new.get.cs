namespace Server.Api;

// GET /api/hello/new
public static class GetHelloNew
{
    public static IResult Handle() =>
        TypedResults.Ok(new { message = "Hello from GET /api/hello/new" });

    // Optional: extra endpoint setup (summary, auth, caching, ...)
    public static void Configure(RouteHandlerBuilder endpoint) =>
        endpoint.WithSummary("GET /api/hello/new");
}
