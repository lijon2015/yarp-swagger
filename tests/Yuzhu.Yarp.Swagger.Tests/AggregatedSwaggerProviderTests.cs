using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class AggregatedSwaggerProviderTests
{
    [Fact]
    public async Task GetSwaggerAsync_WhenDocumentUnknown_ThrowsUnknownSwaggerDocument()
    {
        SwaggerEndpoint known = TestDoubles.CreateEndpoint("known", "known");
        AggregatedSwaggerProvider provider = CreateProvider(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([known], [])),
            new StubAggregator(_ => TestDoubles.CreateDocument("known")));

        UnknownSwaggerDocument exception = await Assert.ThrowsAsync<UnknownSwaggerDocument>(
            () => provider.GetSwaggerAsync("missing"));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSwaggerAsync_WhenKnownDocumentCannotLoad_ThrowsUnavailableException()
    {
        SwaggerEndpoint endpoint = TestDoubles.CreateEndpoint("orders", "orders");
        AggregatedSwaggerProvider provider = CreateProvider(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([endpoint], [])),
            new StubAggregator(_ => throw new InvalidOperationException("backend down")));

        SwaggerDocumentUnavailableException exception =
            await Assert.ThrowsAsync<SwaggerDocumentUnavailableException>(
                () => provider.GetSwaggerAsync("orders"));

        Assert.Equal("orders", exception.DocumentName);
    }

    [Fact]
    public async Task GetSwaggerAsync_WhenCached_ReturnsCachedDocument()
    {
        const string documentName = "cached";
        SwaggerEndpoint endpoint = TestDoubles.CreateEndpoint("cached", documentName);
        InMemoryAggregatedDocumentStore store = new();
        OpenApiDocument cached = TestDoubles.CreateDocument(documentName);
        await store.SetAsync(documentName, cached);

        AggregatedSwaggerProvider provider = CreateProvider(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([endpoint], [])),
            new StubAggregator(_ => TestDoubles.CreateDocument("unexpected")),
            store);

        OpenApiDocument document = await provider.GetSwaggerAsync(documentName);

        Assert.Same(cached, document);
    }

    private static AggregatedSwaggerProvider CreateProvider(
        ISwaggerEndpointDiscoveryService discovery,
        ISwaggerAggregator aggregator,
        IAggregatedDocumentStore? store = null)
    {
        SwaggerDocumentCoordinator coordinator = new(
            discovery,
            aggregator,
            store ?? new InMemoryAggregatedDocumentStore(),
            TestDoubles.OptionsMonitor(),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        return new AggregatedSwaggerProvider(
            coordinator,
            NullLogger<AggregatedSwaggerProvider>.Instance);
    }
}
