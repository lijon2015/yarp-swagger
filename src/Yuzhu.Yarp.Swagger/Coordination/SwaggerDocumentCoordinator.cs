using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Coordination;

/// <summary>
/// Top-level façade. Calls the discovery service, groups endpoints by document name, and
/// either returns a cached document or aggregates a new one through
/// <see cref="ISwaggerAggregator"/>.
/// </summary>
public sealed class SwaggerDocumentCoordinator(
    ISwaggerEndpointDiscoveryService discoveryService,
    ISwaggerAggregator aggregator,
    IAggregatedDocumentStore documentStore,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<SwaggerDocumentCoordinator> logger)
{
    private readonly ISwaggerEndpointDiscoveryService _discoveryService = discoveryService;
    private readonly ISwaggerAggregator _aggregator = aggregator;
    private readonly IAggregatedDocumentStore _documentStore = documentStore;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<SwaggerDocumentCoordinator> _logger = logger;

    /// <summary>
    /// Lists currently known document names. Prefers cached documents (which represent the
    /// last successful refresh) and falls back to a fresh discovery if the cache is empty.
    /// </summary>
    public async ValueTask<IReadOnlyList<string>> GetDocumentNamesAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> cached = _documentStore.GetDocumentNames();
        if (cached.Count > 0)
        {
            return cached;
        }

        SwaggerEndpointDiscoveryResult result = await _discoveryService.DiscoverAsync(
            documentName: null,
            cancellationToken);

        return [.. result.Endpoints
            .Select(static e => e.DocumentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Run a fresh discovery pass and return the structured diagnostics. Used by the
    /// aggregation document middleware to serve <c>/{prefix}/{diagnosticsName}/swagger.json</c>
    /// when <see cref="Configuration.EmptySwaggerEndpointBehavior.DiagnosticEndpoint"/> is on.
    /// </summary>
    public async ValueTask<SwaggerEndpointDiscoveryResult> DiscoverAsync(
        CancellationToken cancellationToken = default) =>
        await _discoveryService.DiscoverAsync(documentName: null, cancellationToken);

    /// <summary>
    /// Resolve a single document by name. Returns a cached copy when available; otherwise
    /// runs a discovery + aggregation pass restricted to the requested document.
    /// </summary>
    public async Task<SwaggerDocumentResolution> ResolveDocumentAsync(
        string documentName,
        CancellationToken cancellationToken = default)
    {
        OpenApiDocument? cached = await _documentStore.GetAsync(documentName, cancellationToken);
        if (cached is not null)
        {
            return SwaggerDocumentResolution.Cached(cached);
        }

        SwaggerEndpointDiscoveryResult discovery = await _discoveryService.DiscoverAsync(
            documentName,
            cancellationToken);

        if (discovery.Endpoints.Count == 0)
        {
            _logger.LogWarning(
                "No endpoints discovered for document '{DocumentName}'",
                documentName);

            return SwaggerDocumentResolution.NotFound(discovery.Diagnostics);
        }

        return await AggregateAndStoreAsync(documentName, discovery.Endpoints, cancellationToken);
    }

    /// <summary>
    /// Discover and aggregate every document. Used by the refresh service.
    /// </summary>
    public async Task<SwaggerRefreshResult> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        SwaggerEndpointDiscoveryResult discovery = await _discoveryService.DiscoverAsync(
            documentName: null,
            cancellationToken);

        if (discovery.Endpoints.Count == 0)
        {
            return new SwaggerRefreshResult(0, 0, 0, 0, discovery.Diagnostics);
        }

        IGrouping<string, SwaggerEndpoint>[] groups = [.. discovery.Endpoints
            .GroupBy(e => e.DocumentName, StringComparer.OrdinalIgnoreCase)];

        int refreshed = 0;
        int failed = 0;

        foreach (IGrouping<string, SwaggerEndpoint> group in groups)
        {
            SwaggerDocumentResolution resolution = await AggregateAndStoreAsync(
                group.Key,
                [.. group],
                cancellationToken);

            if (resolution.Document is not null)
            {
                refreshed++;
                _logger.LogInformation(
                    "Refreshed Swagger document '{DocumentName}' with {PathCount} paths",
                    group.Key,
                    resolution.Document.Paths.Count);
            }
            else
            {
                failed++;
            }
        }

        return new SwaggerRefreshResult(
            EndpointCount: discovery.Endpoints.Count,
            DocumentCount: groups.Length,
            RefreshedCount: refreshed,
            FailedCount: failed,
            Diagnostics: discovery.Diagnostics);
    }

    private async Task<SwaggerDocumentResolution> AggregateAndStoreAsync(
        string documentName,
        IReadOnlyList<SwaggerEndpoint> endpoints,
        CancellationToken cancellationToken)
    {
        try
        {
            AggregationContext context = new()
            {
                DocumentName = documentName,
                Endpoints = endpoints,
                MergeOptions = new SwaggerMergeOptions
                {
                    IncludeFailedServicesWarning = _options.CurrentValue.IncludeFailedServicesWarning,
                },
            };

            OpenApiDocument document = await _aggregator.AggregateAsync(context, cancellationToken);
            await _documentStore.SetAsync(documentName, document, cancellationToken);

            return SwaggerDocumentResolution.Loaded(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to aggregate document '{DocumentName}'",
                documentName);
            return SwaggerDocumentResolution.Failed(ex.Message);
        }
    }
}

/// <summary>
/// Outcome of <see cref="SwaggerDocumentCoordinator.ResolveDocumentAsync"/>.
/// </summary>
public sealed record SwaggerDocumentResolution(
    OpenApiDocument? Document,
    bool FromCache,
    bool EndpointFound,
    string? FailureReason = null,
    IReadOnlyList<SwaggerEndpointDiagnostic>? Diagnostics = null)
{
    /// <summary>The document was returned from cache.</summary>
    public static SwaggerDocumentResolution Cached(OpenApiDocument document) =>
        new(document, FromCache: true, EndpointFound: true);

    /// <summary>The document was aggregated successfully.</summary>
    public static SwaggerDocumentResolution Loaded(OpenApiDocument document) =>
        new(document, FromCache: false, EndpointFound: true);

    /// <summary>No endpoint matches this document name (404).</summary>
    public static SwaggerDocumentResolution NotFound(IReadOnlyList<SwaggerEndpointDiagnostic> diagnostics) =>
        new(Document: null, FromCache: false, EndpointFound: false, Diagnostics: diagnostics);

    /// <summary>An endpoint exists but the document could not be aggregated (5xx).</summary>
    public static SwaggerDocumentResolution Failed(string reason) =>
        new(Document: null, FromCache: false, EndpointFound: true, FailureReason: reason);
}

/// <summary>
/// Outcome of <see cref="SwaggerDocumentCoordinator.RefreshAllAsync"/>.
/// </summary>
public sealed record SwaggerRefreshResult(
    int EndpointCount,
    int DocumentCount,
    int RefreshedCount,
    int FailedCount,
    IReadOnlyList<SwaggerEndpointDiagnostic> Diagnostics);
