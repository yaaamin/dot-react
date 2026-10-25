using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // OpenAPI document at /openapi/v1.json
    app.MapOpenApi();

    // API docs, both built from the OpenAPI document: /swagger and /scalar
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
    app.MapScalarApiReference();
}
else
{
    // Serve the built React app (`bun run build` puts it in wwwroot).
    // In development, Vite serves the React app instead.
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// All API endpoints live under /api so they never clash with React routes.
// MapApiRoutes() is generated from the files in api/: api/users/[id].get.cs → GET /api/users/{id}
var api = app.MapGroup("/api");
api.MapApiRoutes();

// Unknown /api routes should 404, not return index.html.
api.MapFallback(() => Results.NotFound());

if (!app.Environment.IsDevelopment())
{
    // Any other unknown URL (e.g. /weather) is a React route: send index.html
    // and let TanStack Router handle it.
    app.MapFallbackToFile("index.html");
}

app.Run();
