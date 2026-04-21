using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Discovery;

namespace Yuzhu.Yarp.Swagger.Coordination;

/// <summary>
/// Coordinates endpoint discovery, aggregation, and cache storage for Swagger documents.
/// </summary>
public sealed class SwaggerDocumentCoordinator
{
    private readonly IAggregatedDocumentStore _documentStore;
    private readonly ISwaggerEndpointProvider _endpointProvider;
    private readonly ISwaggerAggregator _aggregator;
    private readonly ILogger<SwaggerDocumentCoordinator> _logger;

    public SwaggerDocumentCoordinator(
        IAggregatedDocumentStore documentStore,
        ISwaggerEndpointProvider endpointProvider,
        ISwaggerAggregator aggregator,
        ILogger<SwaggerDocumentCoordinator> logger)
    {
        _documentStore = documentStore;
        _endpointProvider = endpointProvider;
        _aggregator = aggregator;
        _logger = logger;
    }

    public IReadOnlyList<string> GetDocumentNames()
    {
        var cachedNames = _documentStore.GetDocumentNames();
        if (cachedNames.Count > 0)
        {
            return cachedNames;
        }

        return BuildDocumentNames(_endpointProvider.GetEndpoints());
    }

    public async Task<SwaggerDocumentResolution> ResolveDocumentAsync(
        string documentName,
        CancellationToken cancellationToken = default)
    {
        var cachedDocument = await _documentStore.GetAsync(documentName, cancellationToken);
        if (cachedDocument != null)
        {
            return SwaggerDocumentResolution.Cached(cachedDocument);
        }

        var endpoints = _endpointProvider.GetEndpoints(documentName);
        if (endpoints.Count == 0)
        {
            _logger.LogWarning("No endpoints found for document '{DocumentName}'", documentName);
            return SwaggerDocumentResolution.NotFound();
        }

        return await AggregateAndStoreAsync(documentName, endpoints, cancellationToken);
    }

    public async Task<SwaggerRefreshResult> RefreshAllDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoints = _endpointProvider.GetEndpoints();
        if (endpoints.Count == 0)
        {
            _logger.LogDebug("No swagger endpoints discovered");
            return SwaggerRefreshResult.Empty;
        }

        var documentGroups = endpoints
            .GroupBy(SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var refreshedCount = 0;
        var failedCount = 0;

        foreach (var documentGroup in documentGroups)
        {
            var resolution = await AggregateAndStoreAsync(
                documentGroup.Key,
                documentGroup.ToList(),
                cancellationToken);

            if (resolution.Document != null)
            {
                refreshedCount++;

                _logger.LogInformation(
                    "Refreshed swagger document '{DocumentName}' with {PathCount} paths",
                    documentGroup.Key,
                    resolution.Document.Paths.Count);
            }
            else
            {
                failedCount++;
            }
        }

        return new SwaggerRefreshResult(
            endpoints.Count,
            documentGroups.Count,
            refreshedCount,
            failedCount);
    }

    private async Task<SwaggerDocumentResolution> AggregateAndStoreAsync(
        string documentName,
        IReadOnlyList<SwaggerEndpoint> endpoints,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = new AggregationContext
            {
                DocumentName = documentName,
                Endpoints = endpoints
            };

            var document = await _aggregator.AggregateAsync(context, cancellationToken);
            await _documentStore.SetAsync(documentName, document, cancellationToken);

            return SwaggerDocumentResolution.Loaded(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to aggregate document '{DocumentName}'", documentName);
            return SwaggerDocumentResolution.Failed();
        }
    }

    private static IReadOnlyList<string> BuildDocumentNames(IReadOnlyList<SwaggerEndpoint> endpoints)
    {
        return endpoints
            .Select(SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed record SwaggerDocumentResolution(
    OpenApiDocument? Document,
    bool FromCache,
    bool EndpointFound)
{
    public static SwaggerDocumentResolution Cached(OpenApiDocument document) =>
        new(document, FromCache: true, EndpointFound: true);

    public static SwaggerDocumentResolution Loaded(OpenApiDocument document) =>
        new(document, FromCache: false, EndpointFound: true);

    public static SwaggerDocumentResolution NotFound() =>
        new(Document: null, FromCache: false, EndpointFound: false);

    public static SwaggerDocumentResolution Failed() =>
        new(Document: null, FromCache: false, EndpointFound: true);
}

public sealed record SwaggerRefreshResult(
    int EndpointCount,
    int DocumentCount,
    int RefreshedCount,
    int FailedCount)
{
    public static SwaggerRefreshResult Empty { get; } = new(0, 0, 0, 0);
}
