using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery.Metadata;

namespace Yuzhu.Yarp.Swagger.Tests;

internal static class TestDoubles
{
    public static IOptionsMonitor<SwaggerAggregationOptions> OptionsMonitor(
        SwaggerAggregationOptions? options = null) =>
        new StaticOptionsMonitor<SwaggerAggregationOptions>(options ?? new SwaggerAggregationOptions());

    public static OpenApiDocument CreateDocument(string title) =>
        new()
        {
            Info = new OpenApiInfo { Title = title, Version = "v1" },
            Paths = [],
        };

    public static SwaggerEndpoint CreateEndpoint(
        string clusterId,
        string? documentName = null,
        string baseAddress = "https://example.test",
        string? pathPrefix = null,
        string? pathFilter = null,
        bool isMetadataSource = false) =>
        new()
        {
            ClusterId = clusterId,
            DocumentName = documentName ?? clusterId,
            BaseAddress = new Uri(baseAddress),
            SwaggerPath = "/swagger/v1/swagger.json",
            PathPrefix = pathPrefix,
            PathFilter = pathFilter,
            IsMetadataSource = isMetadataSource,
        };

    public static SwaggerClusterCandidate CreateCandidate(
        string clusterId,
        IDictionary<string, string>? metadata = null,
        string? documentName = null,
        object? nativeCluster = null)
    {
        DictionarySwaggerMetadataAccessor accessor = new(
            metadata is null
                ? null
                : new Dictionary<string, string>(metadata, StringComparer.Ordinal));

        string? metadataDocumentName = null;
        if (metadata is not null
            && metadata.TryGetValue(MetadataKeys.DocumentName, out string? value))
        {
            metadataDocumentName = value;
        }

        return new SwaggerClusterCandidate(
            ClusterId: clusterId,
            DocumentName: documentName ?? metadataDocumentName,
            Metadata: accessor,
            NativeCluster: nativeCluster);
    }
}

internal sealed class StaticOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
    where TOptions : class
{
    public TOptions CurrentValue { get; } = value;

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}

internal sealed class StubEndpointSource(IReadOnlyList<SwaggerClusterCandidate> candidates) : ISwaggerEndpointSource
{
    public ValueTask<IReadOnlyList<SwaggerClusterCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(candidates);
}

internal sealed class StubAddressResolver(
    Func<SwaggerClusterDiscoveryContext, SwaggerAddressResolution> resolve) : ISwaggerEndpointAddressResolver
{
    public ValueTask<SwaggerAddressResolution> ResolveAsync(
        SwaggerClusterDiscoveryContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(resolve(context));
}

internal sealed class StubDiscoveryService(SwaggerEndpointDiscoveryResult result) : ISwaggerEndpointDiscoveryService
{
    public ValueTask<SwaggerEndpointDiscoveryResult> DiscoverAsync(
        string? documentName = null,
        CancellationToken cancellationToken = default)
    {
        SwaggerEndpointDiscoveryResult filtered = documentName is null
            ? result
            : new SwaggerEndpointDiscoveryResult(
                [.. result.Endpoints.Where(e => string.Equals(e.DocumentName, documentName, StringComparison.OrdinalIgnoreCase))],
                result.Diagnostics);
        return ValueTask.FromResult(filtered);
    }
}

internal sealed class StubAggregator(Func<AggregationContext, OpenApiDocument> aggregate) : ISwaggerAggregator
{
    public Task<OpenApiDocument> AggregateAsync(
        AggregationContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(aggregate(context));
}
