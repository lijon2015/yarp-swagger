using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Adapts <see cref="SwaggerDocumentCoordinator"/> to Swashbuckle's
/// <see cref="ISwaggerProvider"/> / <see cref="IAsyncSwaggerProvider"/> contracts. Used by
/// the standard <c>UseSwagger()</c> middleware as a fallback; the library's own
/// <c>UseSwaggerAggregationDocuments()</c> middleware exposes the same documents with
/// stronger 200/404/503 semantics.
/// </summary>
public sealed class AggregatedSwaggerProvider(
    SwaggerDocumentCoordinator coordinator,
    ILogger<AggregatedSwaggerProvider> logger) : IAsyncSwaggerProvider, ISwaggerProvider
{
    private readonly SwaggerDocumentCoordinator _coordinator = coordinator;
    private readonly ILogger<AggregatedSwaggerProvider> _logger = logger;

    public async Task<OpenApiDocument> GetSwaggerAsync(
        string documentName,
        string? host = null,
        string? basePath = null) =>
        await ResolveAsync(documentName);

    // Swashbuckle middleware still requires the synchronous contract in DI; bridge here
    // rather than block on the async path inside the request pipeline.
    public OpenApiDocument GetSwagger(
        string documentName,
        string? host = null,
        string? basePath = null) =>
        ResolveAsync(documentName).GetAwaiter().GetResult();

    private async Task<OpenApiDocument> ResolveAsync(string documentName)
    {
        SwaggerDocumentResolution resolution = await _coordinator.ResolveDocumentAsync(documentName);

        if (resolution.Document is not null)
        {
            if (resolution.FromCache)
            {
                SwaggerTelemetry.CacheHitCounter.Add(1,
                    new KeyValuePair<string, object?>("document.name", documentName));
            }

            return resolution.Document;
        }

        if (!resolution.EndpointFound)
        {
            IReadOnlyList<string> known = await _coordinator.GetDocumentNamesAsync();
            _logger.LogWarning(
                "Document '{DocumentName}' is unknown. Known documents: {KnownDocuments}",
                documentName,
                known.Count == 0 ? "(none)" : string.Join(", ", known));
            throw new UnknownSwaggerDocument(documentName, known);
        }

        _logger.LogError(
            "Document '{DocumentName}' is known but could not be loaded: {Reason}",
            documentName,
            resolution.FailureReason ?? "unknown");

        throw new SwaggerDocumentUnavailableException(documentName, resolution.FailureReason);
    }
}
