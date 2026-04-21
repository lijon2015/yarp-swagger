using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class AggregatedSwaggerProviderTests
{
    [Fact]
    public async Task GetSwaggerAsync_WhenDocumentDoesNotExist_ThrowsUnknownSwaggerDocument()
    {
        var knownEndpoint = CreateEndpoint("known-cluster", "known");
        var coordinator = CreateCoordinator(
            new StubSwaggerEndpointProvider([knownEndpoint]),
            new StubSwaggerAggregator(static _ => Task.FromResult(CreateDocument("known"))));

        var provider = new AggregatedSwaggerProvider(
            coordinator,
            NullLogger<AggregatedSwaggerProvider>.Instance);

        var exception = await Assert.ThrowsAsync<UnknownSwaggerDocument>(
            () => provider.GetSwaggerAsync("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("known", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSwaggerAsync_WhenKnownDocumentCannotBeLoaded_ThrowsUnavailableException()
    {
        var endpoint = CreateEndpoint("orders-cluster", "orders");
        var coordinator = CreateCoordinator(
            new StubSwaggerEndpointProvider([endpoint]),
            new StubSwaggerAggregator(static _ => throw new InvalidOperationException("boom")));

        var provider = new AggregatedSwaggerProvider(
            coordinator,
            NullLogger<AggregatedSwaggerProvider>.Instance);

        var exception = await Assert.ThrowsAsync<SwaggerDocumentUnavailableException>(
            () => provider.GetSwaggerAsync("orders"));

        Assert.Equal("orders", exception.DocumentName);
    }

    [Fact]
    public async Task GetSwaggerAsync_WhenDocumentIsCached_ReturnsCachedDocument()
    {
        const string documentName = "cached";
        var endpoint = CreateEndpoint("cached-cluster", documentName);
        var store = new InMemoryAggregatedDocumentStore();
        var cachedDocument = CreateDocument(documentName);
        await store.SetAsync(documentName, cachedDocument);

        var coordinator = new SwaggerDocumentCoordinator(
            store,
            new StubSwaggerEndpointProvider([endpoint]),
            new StubSwaggerAggregator(static _ => Task.FromResult(CreateDocument("should-not-be-used"))),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        var provider = new AggregatedSwaggerProvider(
            coordinator,
            NullLogger<AggregatedSwaggerProvider>.Instance);

        var document = await provider.GetSwaggerAsync(documentName);

        Assert.Same(cachedDocument, document);
    }

    private static SwaggerDocumentCoordinator CreateCoordinator(
        ISwaggerEndpointProvider endpointProvider,
        ISwaggerAggregator aggregator)
    {
        return new SwaggerDocumentCoordinator(
            new InMemoryAggregatedDocumentStore(),
            endpointProvider,
            aggregator,
            NullLogger<SwaggerDocumentCoordinator>.Instance);
    }

    private static SwaggerEndpoint CreateEndpoint(string clusterId, string documentName)
    {
        return new SwaggerEndpoint
        {
            ClusterId = clusterId,
            BaseAddress = new Uri("https://example.test"),
            SwaggerPath = "/swagger/v1/swagger.json",
            DocumentName = documentName
        };
    }

    private static OpenApiDocument CreateDocument(string title)
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = title,
                Version = "v1"
            },
            Paths = new OpenApiPaths()
        };
    }

    private sealed class StubSwaggerAggregator(Func<AggregationContext, Task<OpenApiDocument>> aggregateAsync) : ISwaggerAggregator
    {
        public Task<OpenApiDocument> AggregateAsync(
            AggregationContext context,
            CancellationToken cancellationToken = default)
        {
            return aggregateAsync(context);
        }
    }

    private sealed class StubSwaggerEndpointProvider(IReadOnlyList<SwaggerEndpoint> endpoints) : ISwaggerEndpointProvider
    {
        public IReadOnlyList<SwaggerEndpoint> GetEndpoints() => endpoints;

        public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
        {
            return endpoints
                .Where(endpoint => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, documentName))
                .ToList();
        }
    }
}
