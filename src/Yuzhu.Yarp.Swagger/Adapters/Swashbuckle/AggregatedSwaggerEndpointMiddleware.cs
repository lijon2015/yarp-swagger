using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Library-owned middleware that serves <c>{prefix}/{documentName}/swagger.json</c>. Owns
/// the 200 / 404 / 503 semantics described in the long-term plan; runs in front of
/// Swashbuckle's <c>UseSwagger()</c> so its responses don't depend on Swashbuckle's
/// default exception translation.
/// </summary>
public sealed class AggregatedSwaggerEndpointMiddleware(
    RequestDelegate next,
    SwaggerDocumentCoordinator coordinator,
    IOptions<SwaggerAggregationDocumentEndpointOptions> options,
    ILogger<AggregatedSwaggerEndpointMiddleware> logger)
{
    private const string SwaggerJsonSuffix = "/swagger.json";

    private readonly RequestDelegate _next = next;
    private readonly SwaggerDocumentCoordinator _coordinator = coordinator;
    private readonly SwaggerAggregationDocumentEndpointOptions _options = options.Value;
    private readonly ILogger<AggregatedSwaggerEndpointMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            || !context.Request.Path.StartsWithSegments(_options.RoutePrefix, out PathString remaining))
        {
            await _next(context);
            return;
        }

        if (!TryParseDocumentRoute(remaining, out string? documentName))
        {
            await _next(context);
            return;
        }

        if (string.Equals(documentName, _options.DiagnosticsDocumentName, StringComparison.OrdinalIgnoreCase))
        {
            await HandleDiagnosticsRequestAsync(context);
            return;
        }

        await HandleDocumentRequestAsync(context, documentName);
    }

    private static bool TryParseDocumentRoute(PathString remaining, out string documentName)
    {
        documentName = string.Empty;

        // Expected suffix: /{documentName}/swagger.json
        string value = remaining.Value ?? string.Empty;
        if (!value.EndsWith(SwaggerJsonSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Strip the leading '/' and trailing '/swagger.json'.
        int start = value.Length > 0 && value[0] == '/' ? 1 : 0;
        int length = value.Length - SwaggerJsonSuffix.Length - start;
        if (length <= 0)
        {
            return false;
        }

        ReadOnlySpan<char> trimmed = value.AsSpan(start, length);
        if (trimmed.IsEmpty || trimmed.Contains('/'))
        {
            return false;
        }

        documentName = trimmed.ToString();
        return true;
    }

    private async Task HandleDocumentRequestAsync(HttpContext context, string documentName)
    {
        SwaggerDocumentResolution resolution = await _coordinator.ResolveDocumentAsync(
            documentName,
            context.RequestAborted);

        if (resolution.Document is not null)
        {
            await WriteDocumentAsync(context, resolution.Document);
            return;
        }

        if (!resolution.EndpointFound)
        {
            await WriteNotFoundAsync(context, documentName);
            return;
        }

        await WriteUnavailableAsync(context, documentName, resolution.FailureReason);
    }

    private async Task HandleDiagnosticsRequestAsync(HttpContext context)
    {
        SwaggerEndpointDiscoveryResult discovery =
            await _coordinator.DiscoverAsync(context.RequestAborted);

        OpenApiDocument synthetic = BuildDiagnosticsDocument(discovery);
        await WriteDocumentAsync(context, synthetic);
    }

    private static OpenApiDocument BuildDiagnosticsDocument(SwaggerEndpointDiscoveryResult discovery)
    {
        // The plan requires the diagnostic endpoint to be clearly labeled and not pretend
        // to be an API document. The synthetic doc has no paths and a description that
        // lists every diagnostic so operators can read it from Swagger UI's raw view.
        OpenApiInfo info = new()
        {
            Title = "Swagger Aggregation Diagnostics",
            Version = "diagnostics",
            Description = BuildDiagnosticsDescription(discovery),
        };

        return new OpenApiDocument
        {
            Info = info,
            Paths = [],
            Components = new OpenApiComponents(),
        };
    }

    private static string BuildDiagnosticsDescription(SwaggerEndpointDiscoveryResult discovery)
    {
        if (discovery.Diagnostics.Count == 0)
        {
            return discovery.Endpoints.Count == 0
                ? "No clusters were discovered. Check that any cluster has Swagger:Enabled=true and at least one resolvable destination."
                : $"{discovery.Endpoints.Count} endpoint(s) discovered.";
        }

        IEnumerable<string> lines = discovery.Diagnostics.Select(static d =>
        {
            string scope = d.DocumentName is null
                ? d.ClusterId
                : $"{d.ClusterId} → {d.DocumentName}";
            string location = string.Join(
                ", ",
                new[]
                {
                    d.Address is null ? null : $"address={d.Address}",
                    d.SwaggerPath is null ? null : $"path={d.SwaggerPath}",
                }
                .Where(s => s is not null));
            string trailer = string.IsNullOrEmpty(location) ? string.Empty : $" ({location})";
            return $"- [{d.Severity}] {scope} | {d.Stage}: {d.Message}{trailer}";
        });

        return $"Discovered endpoints: {discovery.Endpoints.Count}.\nDiagnostics:\n" +
               string.Join('\n', lines);
    }

    private async Task WriteDocumentAsync(HttpContext context, OpenApiDocument document)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";

        // Microsoft.OpenApi's async serializer still writes synchronously to the target
        // stream internally. Serialize to an in-memory buffer first, then copy to Kestrel's
        // response stream asynchronously so hosts can keep AllowSynchronousIO disabled.
        using MemoryStream buffer = new();
        await document.SerializeAsJsonAsync(
            buffer,
            _options.OpenApiSpecVersion,
            context.RequestAborted);

        buffer.Position = 0;
        await buffer.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private async Task WriteNotFoundAsync(HttpContext context, string documentName)
    {
        IReadOnlyList<string> known = await _coordinator.GetDocumentNamesAsync(context.RequestAborted);

        _logger.LogWarning(
            "Unknown Swagger document '{DocumentName}'. Known documents: {KnownDocuments}",
            documentName,
            known.Count == 0 ? "(none)" : string.Join(", ", known));

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        object body = _options.IncludeKnownDocumentsOnNotFound
            ? new
            {
                type = "https://yuzhu.io/yarp-swagger/unknown-document",
                title = "Unknown Swagger document",
                status = StatusCodes.Status404NotFound,
                detail = $"Document '{documentName}' is not known to the aggregator.",
                documentName,
                knownDocuments = known,
            }
            : new
            {
                type = "https://yuzhu.io/yarp-swagger/unknown-document",
                title = "Unknown Swagger document",
                status = StatusCodes.Status404NotFound,
                detail = $"Document '{documentName}' is not known to the aggregator.",
                documentName,
            };

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            body,
            cancellationToken: context.RequestAborted);
    }

    private async Task WriteUnavailableAsync(
        HttpContext context,
        string documentName,
        string? reason)
    {
        _logger.LogError(
            "Document '{DocumentName}' is known but unavailable: {Reason}",
            documentName,
            reason ?? "(unknown)");

        context.Response.StatusCode = _options.UnavailableStatusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                type = "https://yuzhu.io/yarp-swagger/document-unavailable",
                title = "Swagger document unavailable",
                status = _options.UnavailableStatusCode,
                detail = $"Document '{documentName}' is known but the aggregator could not produce it.",
                documentName,
                reason,
            },
            cancellationToken: context.RequestAborted);
    }
}
