using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerDocumentCoordinatorTests
{
    [Fact]
    public async Task GetDocumentNamesAsync_PrefersCachedNamesOverDiscovery()
    {
        InMemoryAggregatedDocumentStore store = new();
        await store.SetAsync("orders", TestDoubles.CreateDocument("orders"));
        await store.SetAsync("billing", TestDoubles.CreateDocument("billing"));

        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            store,
            new StubDiscoveryService(SwaggerEndpointDiscoveryResult.Empty),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")));

        IReadOnlyList<string> names = await coordinator.GetDocumentNamesAsync();

        Assert.Equal(new[] { "orders", "billing" }.OrderBy(n => n), names.OrderBy(n => n));
    }

    [Fact]
    public async Task GetDocumentNamesAsync_FallsBackToDiscoveryWhenCacheEmpty()
    {
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing-cluster", "billing");

        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([orders, billing], [])),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")));

        IReadOnlyList<string> names = await coordinator.GetDocumentNamesAsync();

        Assert.Equal(new[] { "orders", "billing" }.OrderBy(n => n), names.OrderBy(n => n));
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenCached_ReturnsCachedDocumentWithoutAggregation()
    {
        InMemoryAggregatedDocumentStore store = new();
        OpenApiDocument cached = TestDoubles.CreateDocument("orders");
        await store.SetAsync("orders", cached);

        bool aggregatorCalled = false;
        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            store,
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([TestDoubles.CreateEndpoint("orders", "orders")], [])),
            new StubAggregator(_ =>
            {
                aggregatorCalled = true;
                return TestDoubles.CreateDocument("unexpected");
            }));

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync("orders");

        Assert.True(resolution.FromCache);
        Assert.Same(cached, resolution.Document);
        Assert.False(aggregatorCalled);
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenDocumentUnknown_ReturnsNotFoundWithDiagnostics()
    {
        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([TestDoubles.CreateEndpoint("known", "known")], [])),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")));

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync("missing");

        Assert.Null(resolution.Document);
        Assert.False(resolution.FromCache);
        Assert.False(resolution.EndpointFound);
    }

    [Fact]
    public async Task ResolveDocumentAsync_WhenAggregatorThrows_ReturnsFailedWithReason()
    {
        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([TestDoubles.CreateEndpoint("orders", "orders")], [])),
            new StubAggregator(_ => throw new InvalidOperationException("backend down")));

        SwaggerDocumentResolution resolution = await coordinator.ResolveDocumentAsync("orders");

        Assert.Null(resolution.Document);
        Assert.True(resolution.EndpointFound);
        Assert.NotNull(resolution.FailureReason);
        Assert.Contains("backend down", resolution.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAllAsync_GroupsByDocumentName_AndCachesEachDocumentSeparately()
    {
        SwaggerEndpoint[] endpoints =
        [
            TestDoubles.CreateEndpoint("orders-primary", "orders"),
            TestDoubles.CreateEndpoint("orders-secondary", "orders"),
            TestDoubles.CreateEndpoint("billing", "billing"),
        ];

        InMemoryAggregatedDocumentStore store = new();
        List<string> aggregatorCalls = [];

        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            store,
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult(endpoints, [])),
            new StubAggregator(ctx =>
            {
                aggregatorCalls.Add(ctx.DocumentName);
                return TestDoubles.CreateDocument(ctx.DocumentName);
            }));

        SwaggerRefreshResult result = await coordinator.RefreshAllAsync();

        Assert.Equal(3, result.EndpointCount);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(2, result.RefreshedCount);
        Assert.Equal(0, result.FailedCount);

        Assert.Equal(new[] { "orders", "billing" }.OrderBy(s => s), aggregatorCalls.OrderBy(s => s));

        Assert.NotNull(await store.GetAsync("orders"));
        Assert.NotNull(await store.GetAsync("billing"));
    }

    [Fact]
    public async Task RefreshAllAsync_WhenNoEndpoints_ReturnsEmptyResultWithDiagnostics()
    {
        SwaggerEndpointDiagnostic diagnostic = new(
            ClusterId: "orders",
            Stage: SwaggerDiagnosticStage.Address,
            Severity: SwaggerDiagnosticSeverity.Warning,
            Message: "no destinations");

        SwaggerDocumentCoordinator coordinator = CreateCoordinator(
            new InMemoryAggregatedDocumentStore(),
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([], [diagnostic])),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")));

        SwaggerRefreshResult result = await coordinator.RefreshAllAsync();

        Assert.Equal(0, result.EndpointCount);
        Assert.Same(diagnostic, Assert.Single(result.Diagnostics));
    }

    private static SwaggerDocumentCoordinator CreateCoordinator(
        IAggregatedDocumentStore store,
        ISwaggerEndpointDiscoveryService discovery,
        ISwaggerAggregator aggregator) =>
        new(
            discovery,
            aggregator,
            store,
            TestDoubles.OptionsMonitor(),
            NullLogger<SwaggerDocumentCoordinator>.Instance);
}
