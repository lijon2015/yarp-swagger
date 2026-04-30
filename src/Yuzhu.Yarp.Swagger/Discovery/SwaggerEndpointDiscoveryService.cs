using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Default <see cref="ISwaggerEndpointDiscoveryService"/>. Walks every registered
/// <see cref="ISwaggerEndpointSource"/>, deduplicates successfully resolved candidates
/// by cluster id, reads metadata, runs the address resolver chain, and emits a structured
/// result with both endpoints and per-cluster diagnostics.
/// </summary>
public sealed class SwaggerEndpointDiscoveryService(
    IEnumerable<ISwaggerEndpointSource> sources,
    IEnumerable<ISwaggerEndpointAddressResolver> resolvers,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<SwaggerEndpointDiscoveryService> logger) : ISwaggerEndpointDiscoveryService
{
    private readonly IReadOnlyList<ISwaggerEndpointSource> _sources = [.. sources];
    private readonly IReadOnlyList<ISwaggerEndpointAddressResolver> _resolvers = [.. resolvers];
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<SwaggerEndpointDiscoveryService> _logger = logger;

    public async ValueTask<SwaggerEndpointDiscoveryResult> DiscoverAsync(
        string? documentName = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = SwaggerTelemetry.ActivitySource.StartActivity("SwaggerDiscovery");
        _ = activity?.SetTag("document.name", documentName);

        SwaggerAggregationOptions options = _options.CurrentValue;

        List<SwaggerEndpoint> endpoints = [];
        List<SwaggerEndpointDiagnostic> diagnostics = [];
        Dictionary<string, string> resolvedClusters = new(StringComparer.OrdinalIgnoreCase);

        foreach (ISwaggerEndpointSource source in _sources)
        {
            string sourceName = source.GetType().Name;
            IReadOnlyList<SwaggerClusterCandidate> candidates;
            try
            {
                candidates = await source.GetCandidatesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Endpoint source {Source} threw during discovery", sourceName);
                diagnostics.Add(new SwaggerEndpointDiagnostic(
                    ClusterId: string.Empty,
                    Stage: SwaggerDiagnosticStage.Source,
                    Severity: SwaggerDiagnosticSeverity.Error,
                    Message: $"Source {sourceName} failed: {ex.Message}"));
                continue;
            }

            foreach (SwaggerClusterCandidate candidate in candidates)
            {
                if (resolvedClusters.TryGetValue(candidate.ClusterId, out string? winningSource))
                {
                    diagnostics.Add(new SwaggerEndpointDiagnostic(
                        ClusterId: candidate.ClusterId,
                        Stage: SwaggerDiagnosticStage.Source,
                        Severity: SwaggerDiagnosticSeverity.Info,
                        Message: $"Skipped duplicate from {sourceName}; first resolved source was {winningSource}",
                        DocumentName: candidate.DocumentName));
                    continue;
                }

                SwaggerEndpoint? endpoint = await TryBuildEndpointAsync(
                    candidate,
                    options.DefaultSwaggerPath,
                    diagnostics,
                    cancellationToken);

                if (endpoint is not null)
                {
                    resolvedClusters[candidate.ClusterId] = sourceName;
                    endpoints.Add(endpoint);
                }
            }
        }

        IReadOnlyList<SwaggerEndpoint> filtered = documentName is null
            ? endpoints
            : [.. endpoints.Where(e => string.Equals(e.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))];

        SwaggerTelemetry.SetEndpointCount(endpoints.Count);
        _ = activity?.SetTag("endpoints.count", endpoints.Count);
        _ = activity?.SetTag("diagnostics.count", diagnostics.Count);

        return new SwaggerEndpointDiscoveryResult(filtered, diagnostics);
    }

    private async ValueTask<SwaggerEndpoint?> TryBuildEndpointAsync(
        SwaggerClusterCandidate candidate,
        string defaultSwaggerPath,
        List<SwaggerEndpointDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        string? enabledRaw = candidate.Metadata.Get(MetadataKeys.Enabled);
        if (!IsTrue(enabledRaw))
        {
            diagnostics.Add(new SwaggerEndpointDiagnostic(
                ClusterId: candidate.ClusterId,
                Stage: SwaggerDiagnosticStage.Metadata,
                Severity: SwaggerDiagnosticSeverity.Info,
                Message: enabledRaw is null
                    ? $"Skipped: metadata key '{MetadataKeys.Enabled}' not set"
                    : $"Skipped: metadata key '{MetadataKeys.Enabled}'='{enabledRaw}'",
                DocumentName: candidate.DocumentName));

            SwaggerTelemetry.DiscoverySkippedCounter.Add(
                1,
                new KeyValuePair<string, object?>("cluster.id", candidate.ClusterId),
                new KeyValuePair<string, object?>("failure.stage", SwaggerDiagnosticStage.Metadata));

            return null;
        }

        SwaggerClusterDiscoveryContext context = new(candidate, defaultSwaggerPath);
        Uri? resolvedAddress = null;
        string? lastSkippedReason = null;

        foreach (ISwaggerEndpointAddressResolver resolver in _resolvers)
        {
            SwaggerAddressResolution outcome;
            try
            {
                outcome = await resolver.ResolveAsync(context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Address resolver {Resolver} threw for cluster {ClusterId}",
                    resolver.GetType().Name,
                    candidate.ClusterId);

                diagnostics.Add(new SwaggerEndpointDiagnostic(
                    ClusterId: candidate.ClusterId,
                    Stage: SwaggerDiagnosticStage.Address,
                    Severity: SwaggerDiagnosticSeverity.Warning,
                    Message: $"Resolver {resolver.GetType().Name} threw: {ex.Message}",
                    DocumentName: candidate.DocumentName));
                continue;
            }

            if (outcome.Resolved && outcome.BaseAddress is not null)
            {
                resolvedAddress = outcome.BaseAddress;
                break;
            }

            if (outcome.SkippedReason is not null)
            {
                lastSkippedReason = outcome.SkippedReason;
            }
        }

        if (resolvedAddress is null)
        {
            diagnostics.Add(new SwaggerEndpointDiagnostic(
                ClusterId: candidate.ClusterId,
                Stage: SwaggerDiagnosticStage.Address,
                Severity: SwaggerDiagnosticSeverity.Warning,
                Message: lastSkippedReason ?? "No address resolver produced a base address",
                DocumentName: candidate.DocumentName));

            SwaggerTelemetry.DiscoverySkippedCounter.Add(
                1,
                new KeyValuePair<string, object?>("cluster.id", candidate.ClusterId),
                new KeyValuePair<string, object?>("failure.stage", SwaggerDiagnosticStage.Address));

            return null;
        }

        string swaggerPath = candidate.Metadata.Get(MetadataKeys.Path) ?? defaultSwaggerPath;
        string effectiveDocumentName = candidate.DocumentName ?? candidate.ClusterId;

        SwaggerEndpoint endpoint = new()
        {
            ClusterId = candidate.ClusterId,
            DocumentName = effectiveDocumentName,
            BaseAddress = resolvedAddress,
            SwaggerPath = swaggerPath,
            PathPrefix = candidate.Metadata.Get(MetadataKeys.Prefix),
            PathFilter = candidate.Metadata.Get(MetadataKeys.PathFilter),
            AccessTokenClient = candidate.Metadata.Get(MetadataKeys.AccessTokenClient),
            IsMetadataSource = IsTrue(candidate.Metadata.Get(MetadataKeys.IsMetadataSource)),
        };

        diagnostics.Add(new SwaggerEndpointDiagnostic(
            ClusterId: candidate.ClusterId,
            Stage: SwaggerDiagnosticStage.Validation,
            Severity: SwaggerDiagnosticSeverity.Info,
            Message: "Endpoint resolved",
            DocumentName: effectiveDocumentName,
            Address: resolvedAddress.ToString(),
            SwaggerPath: swaggerPath));

        _logger.LogDebug(
            "Discovered Swagger endpoint for cluster {ClusterId} (document {DocumentName}) at {Url}",
            candidate.ClusterId,
            effectiveDocumentName,
            endpoint.SwaggerUrl);

        return endpoint;
    }

    internal static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
