using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Serves aggregated Swagger documents through Swashbuckle.
/// </summary>
public sealed class AggregatedSwaggerProvider(
    SwaggerDocumentCoordinator documentCoordinator,
    ILogger<AggregatedSwaggerProvider> logger) : IAsyncSwaggerProvider, ISwaggerProvider
{
    private readonly SwaggerDocumentCoordinator _documentCoordinator = documentCoordinator;
    private readonly ILogger<AggregatedSwaggerProvider> _logger = logger;

    public Task<OpenApiDocument> GetSwaggerAsync(
        string documentName,
        string? host = null,
        string? basePath = null) => GetSwaggerCoreAsync(documentName);

    public OpenApiDocument GetSwagger(
        string documentName,
        string? host = null,
        string? basePath = null) =>
        // Swashbuckle's middleware still requires ISwaggerProvider in DI.
        GetSwaggerCoreAsync(documentName).GetAwaiter().GetResult();

    public IReadOnlyList<string> GetDocumentNames() => _documentCoordinator.GetDocumentNames();

    private async Task<OpenApiDocument> GetSwaggerCoreAsync(string documentName)
    {
        SwaggerDocumentResolution resolution = await _documentCoordinator.ResolveDocumentAsync(documentName);

        if (resolution.Document != null)
        {
            if (resolution.FromCache)
            {
                SwaggerTelemetry.CacheHitCounter.Add(1,
                    new KeyValuePair<string, object?>("document.name", documentName));
            }

            return resolution.Document;
        }

        if (resolution.EndpointFound)
        {
            _logger.LogError(
                "Document '{DocumentName}' is known but could not be loaded",
                documentName);

            throw new SwaggerDocumentUnavailableException(documentName);
        }

        IReadOnlyList<string> knownDocuments = _documentCoordinator.GetDocumentNames();

        _logger.LogWarning(
            "Document '{DocumentName}' not found. Known documents: {KnownDocuments}",
            documentName,
            knownDocuments.Count == 0 ? "(none)" : string.Join(", ", knownDocuments));

        throw new UnknownSwaggerDocument(documentName, knownDocuments);
    }
}
