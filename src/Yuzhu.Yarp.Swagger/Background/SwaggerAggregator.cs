using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// Default <see cref="ISwaggerAggregator"/>: load every endpoint in parallel, run the
/// transformer pipeline against each, then merge into a single document.
/// </summary>
public sealed class SwaggerAggregator(
    ISwaggerDocumentLoader loader,
    ISwaggerDocumentMerger merger,
    IEnumerable<ISwaggerDocumentTransformer> transformers,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<SwaggerAggregator> logger) : ISwaggerAggregator
{
    private readonly ISwaggerDocumentLoader _loader = loader;
    private readonly ISwaggerDocumentMerger _merger = merger;
    private readonly IReadOnlyList<ISwaggerDocumentTransformer> _transformers =
        [.. transformers.OrderBy(t => t.Order)];
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<SwaggerAggregator> _logger = logger;

    public async Task<OpenApiDocument> AggregateAsync(
        AggregationContext context,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = SwaggerTelemetry.ActivitySource.StartActivity("AggregateDocuments");
        _ = activity?.SetTag("document.name", context.DocumentName);
        _ = activity?.SetTag("endpoints.count", context.Endpoints.Count);

        SwaggerAggregationOptions options = _options.CurrentValue;

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.AggregationTimeout);
        CancellationToken aggregationToken = cts.Token;

        SwaggerLoadResult[] results = new SwaggerLoadResult[context.Endpoints.Count];

        try
        {
            await Parallel.ForEachAsync(
                context.Endpoints.Select((endpoint, index) => (endpoint, index)),
                new ParallelOptions
                {
                    CancellationToken = aggregationToken,
                    MaxDegreeOfParallelism = options.MaxParallelism,
                },
                async (item, ct) => results[item.index] = await LoadAndTransformAsync(item.endpoint, ct));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Aggregation timed out for document '{DocumentName}' after {Timeout}",
                context.DocumentName,
                options.AggregationTimeout);

            throw new TimeoutException(
                $"Aggregation timed out after {options.AggregationTimeout.TotalSeconds}s for document '{context.DocumentName}'");
        }

        int success = results.Count(static r => r.IsSuccess);
        _logger.LogDebug(
            "Loaded {SuccessCount}/{TotalCount} documents for '{DocumentName}'",
            success,
            results.Length,
            context.DocumentName);

        // Long-term plan: known-but-unavailable must surface as 5xx, not as a 200 empty
        // document. The merger will happily produce an empty doc when every load failed,
        // so guard here. The coordinator catches this and returns Failed, which the
        // aggregation middleware translates into the configured unavailable status.
        if (success == 0 && results.Length > 0)
        {
            string reason = SummarizeFailures(results);
            _logger.LogError(
                "All {Count} backend(s) failed to load for document '{DocumentName}': {Reason}",
                results.Length,
                context.DocumentName,
                reason);

            throw new SwaggerAggregationFailedException(context.DocumentName, results.Length, reason);
        }

        return _merger.Merge(context.DocumentName, results, context.MergeOptions);
    }

    private static string SummarizeFailures(IReadOnlyList<SwaggerLoadResult> results)
    {
        // Short, structured summary for the failure reason that flows into 5xx bodies and
        // logs. Per-cluster details live in load.failure metrics; this is the operator's
        // first signal.
        IEnumerable<string> entries = results.Select(static r =>
        {
            string stage = r.FailureStage ?? "unknown";
            string status = r.HttpStatusCode is int code ? code.ToString() : "-";
            return $"{r.Endpoint.ClusterId}|{stage}|{status}";
        });

        return string.Join(", ", entries);
    }

    private async Task<SwaggerLoadResult> LoadAndTransformAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        SwaggerLoadResult loadResult = await _loader.LoadAsync(endpoint, cancellationToken);
        if (!loadResult.IsSuccess || loadResult.Document is null)
        {
            return loadResult;
        }

        OpenApiDocument document = loadResult.Document;
        TransformContext transformContext = new()
        {
            ClusterId = endpoint.ClusterId,
            Endpoint = endpoint,
        };

        foreach (ISwaggerDocumentTransformer transformer in _transformers)
        {
            try
            {
                document = await transformer.TransformAsync(document, transformContext, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Transformer {Transformer} failed for cluster {ClusterId}; continuing with previous document",
                    transformer.GetType().Name,
                    endpoint.ClusterId);
            }
        }

        return loadResult with { Document = document };
    }
}
