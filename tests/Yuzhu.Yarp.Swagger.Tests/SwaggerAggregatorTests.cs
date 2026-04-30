using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Background;
using Yuzhu.Yarp.Swagger.Merging;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerAggregatorTests
{
    [Fact]
    public async Task AggregateAsync_WhenAllLoadsFail_ThrowsAggregationFailedException()
    {
        // Regression for a v3.0.0 review finding: when every backend fails to load, the
        // merger would happily produce an empty doc and the middleware would return a 200
        // with a near-empty OpenAPI document. The aggregator must surface this as a
        // failure so the coordinator returns a "known-but-unavailable" resolution and the
        // middleware emits 503.
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing", "orders");

        SwaggerAggregator aggregator = CreateAggregator(
            loader: new StubLoader((endpoint, _) => Task.FromResult(new SwaggerLoadResult
            {
                Endpoint = endpoint,
                FailureStage = "http",
                HttpStatusCode = 503,
                ErrorMessage = "down",
            })));

        SwaggerAggregationFailedException exception = await Assert.ThrowsAsync<SwaggerAggregationFailedException>(
            () => aggregator.AggregateAsync(new AggregationContext
            {
                DocumentName = "orders",
                Endpoints = [orders, billing],
            }));

        Assert.Equal("orders", exception.DocumentName);
        Assert.Equal(2, exception.EndpointCount);
        Assert.Contains("orders", exception.Reason, StringComparison.Ordinal);
        Assert.Contains("billing", exception.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AggregateAsync_WhenOneOfManySucceeds_ReturnsMergedDocument()
    {
        // Partial success is expected to return the merged document: dropping the whole
        // group on a single failed backend would be too aggressive.
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing", "orders");

        SwaggerAggregator aggregator = CreateAggregator(
            loader: new StubLoader((endpoint, _) =>
                Task.FromResult(endpoint.ClusterId == "billing"
                    ? new SwaggerLoadResult
                    {
                        Endpoint = endpoint,
                        FailureStage = "http",
                        ErrorMessage = "down",
                    }
                    : new SwaggerLoadResult
                    {
                        Endpoint = endpoint,
                        Document = TestDoubles.CreateDocument("orders"),
                        HttpStatusCode = 200,
                    })));

        OpenApiDocument document = await aggregator.AggregateAsync(new AggregationContext
        {
            DocumentName = "orders",
            Endpoints = [orders, billing],
        });

        Assert.NotNull(document);
    }

    private static SwaggerAggregator CreateAggregator(ISwaggerDocumentLoader loader) =>
        new(
            loader,
            new DefaultSwaggerDocumentMerger(NullLogger<DefaultSwaggerDocumentMerger>.Instance),
            transformers: [],
            TestDoubles.OptionsMonitor(),
            NullLogger<SwaggerAggregator>.Instance);

    private sealed class StubLoader(Func<SwaggerEndpoint, CancellationToken, Task<SwaggerLoadResult>> load) : ISwaggerDocumentLoader
    {
        public Task<SwaggerLoadResult> LoadAsync(
            SwaggerEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            load(endpoint, cancellationToken);
    }
}
