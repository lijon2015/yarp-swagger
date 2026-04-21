using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerDocumentCoordinatorTests
{
    [Fact]
    public async Task GetDocumentNames_WhenCacheHasEntries_ReturnsCachedNames()
    {
        InMemoryAggregatedDocumentStore store = new InMemoryAggregatedDocumentStore();
        await store.SetAsync("orders", CreateDocument("orders"));
        await store.SetAsync("billing", CreateDocument("billing"));

        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            store,
            new StubEndpointProvider([CreateEndpoint("c", "untouched")]),
            new StubAggregator(static _ => throw new InvalidOperationException("aggregator must not run")),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        IReadOnlyList<string> names = coordinator.GetDocumentNames();

        Assert.Equal(new[] { "orders", "billing" }.OrderBy(x => x), names.OrderBy(x => x));
    }

    [Fact]
    public void GetDocumentNames_WhenCacheIsEmpty_DerivesFromEndpointsAndDeduplicates()
    {
        SwaggerEndpoint[] endpoints =
        [
            CreateEndpoint("cluster-a", "orders"),
            CreateEndpoint("cluster-b", "ORDERS"), // case-insensitive dup
            CreateEndpoint("cluster-c", null, clusterIdAsDocumentFallback: "fallback-name"),
        ];

        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubEndpointProvider(endpoints),
            new StubAggregator(static _ => throw new InvalidOperationException("aggregator must not run")),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        IReadOnlyList<string> names = coordinator.GetDocumentNames();

        Assert.Equal(2, names.Count);
        Assert.Contains("orders", names, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("fallback-name", names, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAllDocumentsAsync_WhenNoEndpoints_ReturnsEmptyResult()
    {
        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubEndpointProvider([]),
            new StubAggregator(static _ => throw new InvalidOperationException("aggregator must not run")),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerRefreshResult result = await coordinator.RefreshAllDocumentsAsync();

        Assert.Same(SwaggerRefreshResult.Empty, result);
    }

    [Fact]
    public async Task RefreshAllDocumentsAsync_GroupsByEffectiveDocumentName_AndStoresResults()
    {
        SwaggerEndpoint[] endpoints =
        [
            CreateEndpoint("orders-primary", "orders"),
            CreateEndpoint("orders-secondary", "orders"), // same document group
            CreateEndpoint("billing", null),              // document name = cluster id
        ];

        InMemoryAggregatedDocumentStore store = new InMemoryAggregatedDocumentStore();
        List<string> aggregatorCalls = [];

        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            store,
            new StubEndpointProvider(endpoints),
            new StubAggregator(ctx =>
            {
                aggregatorCalls.Add(ctx.DocumentName!);
                return Task.FromResult(CreateDocument(ctx.DocumentName!));
            }),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerRefreshResult result = await coordinator.RefreshAllDocumentsAsync();

        Assert.Equal(3, result.EndpointCount);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(2, result.RefreshedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(2, aggregatorCalls.Count);
        Assert.Contains("orders", aggregatorCalls);
        Assert.Contains("billing", aggregatorCalls);

        OpenApiDocument? cachedOrders = await store.GetAsync("orders");
        OpenApiDocument? cachedBilling = await store.GetAsync("billing");
        Assert.NotNull(cachedOrders);
        Assert.NotNull(cachedBilling);
    }

    [Fact]
    public async Task RefreshAllDocumentsAsync_WhenOneDocumentFails_CountsIndividualOutcomes()
    {
        SwaggerEndpoint[] endpoints =
        [
            CreateEndpoint("orders", "orders"),
            CreateEndpoint("billing", "billing"),
        ];

        InMemoryAggregatedDocumentStore store = new InMemoryAggregatedDocumentStore();
        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            store,
            new StubEndpointProvider(endpoints),
            new StubAggregator(ctx => ctx.DocumentName == "billing"
                ? throw new InvalidOperationException("backend down")
                : Task.FromResult(CreateDocument(ctx.DocumentName!))),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerRefreshResult result = await coordinator.RefreshAllDocumentsAsync();

        Assert.Equal(2, result.EndpointCount);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(1, result.RefreshedCount);
        Assert.Equal(1, result.FailedCount);

        Assert.NotNull(await store.GetAsync("orders"));
        Assert.Null(await store.GetAsync("billing"));
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenEndpointMissing_ReturnsNotFound()
    {
        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubEndpointProvider([CreateEndpoint("known", "known")]),
            new StubAggregator(static _ => throw new InvalidOperationException("aggregator must not run")),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync("missing");

        Assert.Null(resolution.Document);
        Assert.False(resolution.FromCache);
        Assert.False(resolution.EndpointFound);
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenAggregatorThrows_ReturnsFailedWithEndpointFoundTrue()
    {
        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubEndpointProvider([CreateEndpoint("orders", "orders")]),
            new StubAggregator(static _ => throw new InvalidOperationException("boom")),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync("orders");

        Assert.Null(resolution.Document);
        Assert.False(resolution.FromCache);
        Assert.True(resolution.EndpointFound);
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenCached_DoesNotInvokeAggregator()
    {
        const string documentName = "orders";
        InMemoryAggregatedDocumentStore store = new InMemoryAggregatedDocumentStore();
        OpenApiDocument cached = CreateDocument(documentName);
        await store.SetAsync(documentName, cached);

        bool aggregatorCalled = false;
        SwaggerDocumentCoordinator coordinator = new SwaggerDocumentCoordinator(
            store,
            new StubEndpointProvider([CreateEndpoint("orders", documentName)]),
            new StubAggregator(_ =>
            {
                aggregatorCalled = true;
                return Task.FromResult(CreateDocument("unexpected"));
            }),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync(documentName);

        Assert.True(resolution.FromCache);
        Assert.Same(cached, resolution.Document);
        Assert.False(aggregatorCalled);
    }

    private static SwaggerEndpoint CreateEndpoint(
        string clusterId,
        string? documentName,
        string? clusterIdAsDocumentFallback = null)
    {
        return new SwaggerEndpoint
        {
            ClusterId = clusterIdAsDocumentFallback ?? clusterId,
            BaseAddress = new Uri("https://example.test"),
            SwaggerPath = "/swagger/v1/swagger.json",
            DocumentName = documentName,
        };
    }

    private static OpenApiDocument CreateDocument(string title)
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = title, Version = "v1" },
            Paths = [],
        };
    }

    private sealed class StubEndpointProvider(IReadOnlyList<SwaggerEndpoint> endpoints) : ISwaggerEndpointProvider
    {
        public IReadOnlyList<SwaggerEndpoint> GetEndpoints() => endpoints;

        public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName) => [.. endpoints.Where(endpoint => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, documentName))];
    }

    private sealed class StubAggregator(Func<AggregationContext, Task<OpenApiDocument>> aggregateAsync) : ISwaggerAggregator
    {
        public Task<OpenApiDocument> AggregateAsync(AggregationContext context, CancellationToken cancellationToken = default) => aggregateAsync(context);
    }
}
