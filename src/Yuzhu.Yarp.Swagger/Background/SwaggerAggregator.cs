using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// Swagger document aggregation service.
/// </summary>
public sealed class SwaggerAggregator : ISwaggerAggregator
{
    private readonly ISwaggerDocumentLoader _loader;
    private readonly ISwaggerDocumentMerger _merger;
    private readonly IReadOnlyList<ISwaggerDocumentTransformer> _transformers;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options;
    private readonly ILogger<SwaggerAggregator> _logger;

    public SwaggerAggregator(
        ISwaggerDocumentLoader loader,
        ISwaggerDocumentMerger merger,
        IEnumerable<ISwaggerDocumentTransformer> transformers,
        IOptionsMonitor<SwaggerAggregationOptions> options,
        ILogger<SwaggerAggregator> logger)
    {
        _loader = loader;
        _merger = merger;
        _transformers = transformers.OrderBy(t => t.Order).ToList();
        _options = options;
        _logger = logger;
    }

    public async Task<OpenApiDocument> AggregateAsync(
        AggregationContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = SwaggerTelemetry.ActivitySource.StartActivity("AggregateDocuments");
        activity?.SetTag("document.name", context.DocumentName);
        activity?.SetTag("endpoints.count", context.Endpoints.Count);

        var options = _options.CurrentValue;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.AggregationTimeout);
        var aggregationToken = cts.Token;

        try
        {
            var loadResults = new SwaggerLoadResult[context.Endpoints.Count];
            var indexedEndpoints = context.Endpoints.Select((endpoint, index) => (endpoint, index));

            await Parallel.ForEachAsync(
                indexedEndpoints,
                new ParallelOptions
                {
                    CancellationToken = aggregationToken,
                    MaxDegreeOfParallelism = options.MaxParallelism
                },
                async (item, ct) =>
                {
                    loadResults[item.index] = await LoadAndTransformAsync(item.endpoint, ct);
                });

            _logger.LogDebug(
                "Loaded {SuccessCount}/{TotalCount} documents for '{DocumentName}'",
                loadResults.Count(r => r.IsSuccess),
                loadResults.Length,
                context.DocumentName);

            return _merger.Merge(loadResults, context.MergeOptions);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Aggregation timeout for '{DocumentName}' after {Timeout}",
                context.DocumentName,
                options.AggregationTimeout);

            SwaggerTelemetry.LoadFailureCounter.Add(1,
                new KeyValuePair<string, object?>("document.name", context.DocumentName),
                new KeyValuePair<string, object?>("error.type", "aggregation_timeout"));

            throw new TimeoutException(
                $"Aggregation timed out after {options.AggregationTimeout.TotalSeconds} seconds for document '{context.DocumentName}'");
        }
    }

    private async Task<SwaggerLoadResult> LoadAndTransformAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var loadResult = await _loader.LoadAsync(endpoint, cancellationToken);

        if (!loadResult.IsSuccess || loadResult.Document == null)
        {
            return loadResult;
        }

        var document = loadResult.Document;
        var transformContext = new TransformContext
        {
            ClusterId = endpoint.ClusterId,
            Endpoint = endpoint
        };

        foreach (var transformer in _transformers)
        {
            try
            {
                document = await transformer.TransformAsync(document, transformContext, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Transformer {TransformerType} failed for {ClusterId}",
                    transformer.GetType().Name,
                    endpoint.ClusterId);
            }
        }

        return loadResult with { Document = document };
    }
}
