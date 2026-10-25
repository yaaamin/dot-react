using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Server.Generators;

/// <summary>
/// File-based API routing, in the style of Nitro's server/api folder.
///
///   api/hello.get.cs           → GET    /api/hello
///   api/users/index.get.cs     → GET    /api/users
///   api/users/index.post.cs    → POST   /api/users
///   api/users/[id].get.cs      → GET    /api/users/{id}
///   api/files/[...path].get.cs → GET    /api/files/{*path}
///
/// Each file holds a class with a static <c>Handle</c> method (the endpoint handler) and,
/// optionally, a static <c>Configure(RouteHandlerBuilder)</c> method for extra endpoint setup.
/// The generator emits <c>MapApiRoutes()</c>, which Program.cs calls on the /api group.
/// </summary>
[Generator]
public sealed class ApiRouteGenerator : IIncrementalGenerator
{
    const string ApiFolder = "api";

    static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete", "head", "options"];

    static readonly DiagnosticDescriptor DuplicateRoute = new(
        "API001", "Duplicate API route",
        "'{0} {1}' is defined by both '{2}' and '{3}'",
        "ApiRoutes", DiagnosticSeverity.Error, isEnabledByDefault: true);

    static readonly DiagnosticDescriptor MissingHandler = new(
        "API002", "API file has no handler",
        "'{0}' is in the api folder but has no static Handle method, so no route was generated",
        "ApiRoutes", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    static readonly DiagnosticDescriptor MultipleHandlers = new(
        "API003", "API file has more than one handler",
        "'{0}' has more than one static Handle method; keep exactly one per file",
        "ApiRoutes", DiagnosticSeverity.Error, isEnabledByDefault: true);

    static readonly DiagnosticDescriptor InaccessibleHandler = new(
        "API004", "API handler is not accessible",
        "'{0}' must be a public or internal top-level class so the generated routes can call it",
        "ApiRoutes", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var apiDir = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
        {
            options.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
            return Normalize(projectDir ?? "").TrimEnd('/') + "/" + ApiFolder + "/";
        });

        var handlers = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is MethodDeclarationSyntax { Identifier.ValueText: "Handle" } m
                    && m.Modifiers.Any(t => t.ValueText == "static"),
                static (ctx, ct) => ToHandler(ctx, ct))
            .Where(static h => h is not null)
            .Select(static (h, _) => h!.Value)
            .Collect()
            .Combine(apiDir)
            .Select(static (pair, _) => pair.Left
                .Where(h => h.FilePath.StartsWith(pair.Right, StringComparison.OrdinalIgnoreCase))
                .Select(h => h with { RelativePath = h.FilePath.Substring(pair.Right.Length) })
                .ToImmutableArray());

        // File paths under api/, so files without a handler can be reported.
        var apiFiles = context.CompilationProvider
            .Combine(apiDir)
            .Select(static (pair, _) => pair.Left.SyntaxTrees
                .Select(t => Normalize(t.FilePath))
                .Where(p => p.StartsWith(pair.Right, StringComparison.OrdinalIgnoreCase))
                .Select(p => (Path: p, Relative: p.Substring(pair.Right.Length)))
                .ToImmutableArray());

        context.RegisterSourceOutput(handlers.Combine(apiFiles), static (spc, pair) => Emit(spc, pair.Left, pair.Right));
    }

    static Handler? ToHandler(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not IMethodSymbol method)
            return null;

        var type = method.ContainingType;
        var hasConfigure = type.GetMembers("Configure").OfType<IMethodSymbol>()
            .Any(m => m.IsStatic && m.Parameters.Length == 1 && m.Parameters[0].Type.Name == "RouteHandlerBuilder");
        var accessible = type.ContainingType is null
            && !type.IsFileLocal
            && type.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;

        return new Handler(
            Normalize(ctx.Node.SyntaxTree.FilePath),
            RelativePath: "",
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            hasConfigure,
            accessible,
            ctx.Node.GetLocation());
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<Handler> handlers, ImmutableArray<(string Path, string Relative)> files)
    {
        var withHandler = new HashSet<string>(handlers.Select(h => h.FilePath), StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(f => !withHandler.Contains(f.Path)))
            spc.ReportDiagnostic(Diagnostic.Create(MissingHandler,
                Location.Create(file.Path, default, default), ApiFolder + "/" + file.Relative));

        var routes = new List<Route>();
        foreach (var group in handlers.GroupBy(h => h.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                foreach (var h in group)
                    spc.ReportDiagnostic(Diagnostic.Create(MultipleHandlers, h.Location, ApiFolder + "/" + h.RelativePath));
                continue;
            }

            var handler = group.First();
            if (!handler.Accessible)
            {
                spc.ReportDiagnostic(Diagnostic.Create(InaccessibleHandler, handler.Location, handler.TypeName));
                continue;
            }

            routes.Add(ParseRoute(handler));
        }

        var seen = new Dictionary<string, Route>();
        foreach (var route in routes)
        {
            var key = route.Method + " " + route.Pattern.ToLowerInvariant();
            if (seen.TryGetValue(key, out var existing))
                spc.ReportDiagnostic(Diagnostic.Create(DuplicateRoute, route.Handler.Location,
                    route.Method, "/api" + route.Pattern, existing.Handler.RelativePath, route.Handler.RelativePath));
            else
                seen[key] = route;
        }

        spc.AddSource("ApiRoutes.g.cs", SourceText.From(Render(seen.Values), Encoding.UTF8));
    }

    /// <summary>"users/[id].get.cs" → GET /users/{id}</summary>
    static Route ParseRoute(Handler handler)
    {
        var segments = handler.RelativePath.Substring(0, handler.RelativePath.Length - ".cs".Length).Split('/').ToList();

        var method = "GET";
        var last = segments[segments.Count - 1];
        var dot = last.LastIndexOf('.');
        if (dot > 0 && HttpMethods.Contains(last.Substring(dot + 1).ToLowerInvariant()))
        {
            method = last.Substring(dot + 1).ToUpperInvariant();
            segments[segments.Count - 1] = last.Substring(0, dot);
        }

        if (segments[segments.Count - 1].Equals("index", StringComparison.OrdinalIgnoreCase))
            segments.RemoveAt(segments.Count - 1);

        var pattern = "/" + string.Join("/", segments.Select(s =>
            Regex.Replace(Regex.Replace(s, @"\[\.\.\.(\w+)\]", "{*$1}"), @"\[(\w+)\]", "{$1}")));

        // Operation id, e.g. GetUsersById, and a tag from the first static segment, for Swagger/Scalar grouping.
        var name = Pascal(method.ToLowerInvariant()) + string.Concat(segments.Select(s =>
            Regex.Replace(s, @"\[(?:\.\.\.)?(\w+)\]", m => "By" + Pascal(m.Groups[1].Value)))
            .Select(s => string.Concat(Regex.Split(s, @"[^A-Za-z0-9]+").Select(Pascal))));
        var tag = segments.FirstOrDefault(s => !s.Contains("[")) ?? "root";

        return new Route(method, pattern, name, tag, handler);
    }

    static string Render(IEnumerable<Route> routes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated> by Server.Generators.ApiRouteGenerator from the files in server/api. Don't edit. </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("namespace Microsoft.AspNetCore.Builder;");
        sb.AppendLine();
        sb.AppendLine("internal static class GeneratedApiRoutes");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapApiRoutes(this global::Microsoft.AspNetCore.Routing.IEndpointRouteBuilder api)");
        sb.AppendLine("    {");
        foreach (var r in routes.OrderBy(r => r.Pattern, StringComparer.Ordinal).ThenBy(r => r.Method, StringComparer.Ordinal))
        {
            sb.AppendLine($"        // {r.Method} /api{r.Pattern}  ←  api/{r.Handler.RelativePath}");
            sb.AppendLine("        {");
            sb.AppendLine($"            var endpoint = api.MapMethods(\"{r.Pattern}\", [\"{r.Method}\"], {r.Handler.TypeName}.Handle)");
            sb.AppendLine($"                .WithName(\"{r.Name}\")");
            sb.AppendLine($"                .WithTags(\"{r.Tag}\");");
            if (r.Handler.HasConfigure)
                sb.AppendLine($"            {r.Handler.TypeName}.Configure(endpoint);");
            sb.AppendLine("        }");
        }
        sb.AppendLine("        return api;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static string Normalize(string path) => path.Replace('\\', '/');

    static string Pascal(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    readonly record struct Handler(string FilePath, string RelativePath, string TypeName, bool HasConfigure, bool Accessible, Location Location);

    readonly record struct Route(string Method, string Pattern, string Name, string Tag, Handler Handler);
}
