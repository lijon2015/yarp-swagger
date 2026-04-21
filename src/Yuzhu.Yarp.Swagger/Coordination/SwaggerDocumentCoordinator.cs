using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Discovery;

namespace Yuzhu.Yarp.Swagger.Coordination;

/// <summary>
/// Coordinates endpoint discovery, aggregation, and cache storage for Swagger documents.
/// </summary>
public sealed class SwaggerDocumentCoordinator(
    IAggregatedDocumentStore documentStore,
    ISwaggerEndpointProvider endpointProvider,
    ISwaggerAggregator aggregator,
    ILogger<SwaggerDocumentCoordinator> logger)
{
    private readonly IAggregatedDocumentStore _documentStore = documentStore;
    private readonly ISwaggerEndpointProvider _endpointProvider = endpointProvider;
    private readonly ISwaggerAggregator _aggregator = aggregator;
    private readonly ILogger<SwaggerDocumentCoordinator> _logger = logger;

    public IReadOnlyList<string> GetDocumentNames()
    {
        IReadOnlyList<string> cachedNames = _documentStore.GetDocumentNames();
        return cachedNames.Count > 0 ? cachedNames : BuildDocumentNames(_endpointProvider.GetEndpoints());
    }

    public async Task<SwaggerDocumentResolution> ResolveDocumentAsync(
        string documentName,
        CancellationToken cancellationToken = default)
    {
        OpenApiDocument? cachedDocument = await _documentStore.GetAsync(documentName, cancellationToken);
        if (cachedDocument != null)
        {
            return SwaggerDocumentResolution.Cached(cachedDocument);
        }

        IReadOnlyList<SwaggerEndpoint> endpoints = _endpointProvider.GetEndpoints(documentName);
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
        IReadOnlyList<SwaggerEndpoint> endpoints = _endpointProvider.GetEndpoints();
        if (endpoints.Count == 0)
        {
            _logger.LogDebug("No swagger endpoints discovered");
            return SwaggerRefreshResult.Empty;
        }

        List<IGrouping<string, SwaggerEndpoint>> documentGroups = [.. endpoints.GroupBy(SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName, StringComparer.OrdinalIgnoreCase)];

        int refreshedCount = 0;
        int failedCount = 0;

        foreach (IGrouping<string, SwaggerEndpoint>? documentGroup in documentGroups)
        {
            SwaggerDocumentResolution resolution = await AggregateAndStoreAsync(
                documentGroup.Key,
                [.. documentGroup],
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
            AggregationContext context = new AggregationContext
            {
                DocumentName = documentName,
                Endpoints = endpoints
            };

            OpenApiDocument document = await _aggregator.AggregateAsync(context, cancellationToken);
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
        return [.. endpoints
            .Select(SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
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
