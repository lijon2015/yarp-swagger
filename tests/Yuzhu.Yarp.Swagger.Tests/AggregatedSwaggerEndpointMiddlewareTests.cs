using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class AggregatedSwaggerEndpointMiddlewareTests
{
    [Fact]
    public async Task Returns200_WithJsonBody_WhenDocumentResolves()
    {
        const string documentName = "orders";
        SwaggerEndpoint endpoint = TestDoubles.CreateEndpoint("orders-cluster", documentName);
        InMemoryAggregatedDocumentStore store = new();
        OpenApiDocument cached = TestDoubles.CreateDocument(documentName);
        await store.SetAsync(documentName, cached);

        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([endpoint], [])),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")),
            store);

        HttpContext context = BuildContext($"/swagger/{documentName}/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("application/json", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Returns404_WithKnownDocumentList_WhenDocumentUnknown()
    {
        SwaggerEndpoint known = TestDoubles.CreateEndpoint("known-cluster", "known");
        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([known], [])),
            new StubAggregator(_ => TestDoubles.CreateDocument("known")));

        HttpContext context = BuildContext("/swagger/missing/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("missing", body.RootElement.GetProperty("documentName").GetString());
        JsonElement known_documents = body.RootElement.GetProperty("knownDocuments");
        Assert.Equal(JsonValueKind.Array, known_documents.ValueKind);
        Assert.Equal("known", known_documents[0].GetString());
    }

    [Fact]
    public async Task Returns503_WhenDocumentKnownButUnavailable()
    {
        SwaggerEndpoint endpoint = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([endpoint], [])),
            new StubAggregator(_ => throw new InvalidOperationException("backend down")));

        HttpContext context = BuildContext("/swagger/orders/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("orders", body.RootElement.GetProperty("documentName").GetString());
    }

    [Fact]
    public async Task PassesThrough_WhenPathDoesNotMatch()
    {
        bool nextCalled = false;
        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(SwaggerEndpointDiscoveryResult.Empty),
            new StubAggregator(_ => TestDoubles.CreateDocument("anything")),
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        HttpContext context = BuildContext("/api/foo");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task PassesThrough_WhenPathIsBareSwaggerJson()
    {
        // The middleware only handles /{prefix}/{documentName}/swagger.json. A bare
        // /swagger/swagger.json must fall through, so Swashbuckle's middleware can decide
        // what to do (typically 404).
        bool nextCalled = false;
        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(SwaggerEndpointDiscoveryResult.Empty),
            new StubAggregator(_ => TestDoubles.CreateDocument("anything")),
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        HttpContext context = BuildContext("/swagger/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task DiagnosticsPath_Returns200_WithSyntheticOpenApiDocument()
    {
        // Diagnostics path (default "diagnostics") must be served by the middleware as a
        // real OpenAPI document so Swagger UI can render it. It must NOT 404 just because
        // there is no aggregated cluster called "diagnostics".
        SwaggerEndpointDiagnostic diagnostic = new(
            ClusterId: "orders",
            Stage: SwaggerDiagnosticStage.Metadata,
            Severity: SwaggerDiagnosticSeverity.Info,
            Message: "Skipped: metadata key 'Swagger:Enabled' not set");

        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([], [diagnostic])),
            new StubAggregator(_ => throw new InvalidOperationException("aggregator must not run")));

        HttpContext context = BuildContext("/swagger/diagnostics/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("application/json", context.Response.ContentType, StringComparison.OrdinalIgnoreCase);

        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            "Swagger Aggregation Diagnostics",
            body.RootElement.GetProperty("info").GetProperty("title").GetString());

        string description = body.RootElement.GetProperty("info").GetProperty("description").GetString()!;
        Assert.Contains("orders", description, StringComparison.Ordinal);
        Assert.Contains("metadata", description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndToEnd_WhenAllBackendsFailToLoad_Returns503()
    {
        // Wires up the real coordinator + aggregator + merger with a loader that fails
        // every request, then verifies the middleware returns 503 instead of a 200 with
        // an empty merged document. This is the case the aggregator-level test guards
        // against, exercised through the request pipeline.
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing-cluster", "orders");

        Yuzhu.Yarp.Swagger.Background.SwaggerAggregator aggregator = new(
            loader: new AlwaysFailingLoader(),
            merger: new Yuzhu.Yarp.Swagger.Merging.DefaultSwaggerDocumentMerger(
                NullLogger<Yuzhu.Yarp.Swagger.Merging.DefaultSwaggerDocumentMerger>.Instance),
            transformers: [],
            options: TestDoubles.OptionsMonitor(),
            logger: NullLogger<Yuzhu.Yarp.Swagger.Background.SwaggerAggregator>.Instance);

        SwaggerDocumentCoordinator coordinator = new(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([orders, billing], [])),
            aggregator,
            new InMemoryAggregatedDocumentStore(),
            TestDoubles.OptionsMonitor(),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        AggregatedSwaggerEndpointMiddleware middleware = new(
            next: _ => Task.CompletedTask,
            coordinator,
            Options.Create(new SwaggerAggregationDocumentEndpointOptions()),
            NullLogger<AggregatedSwaggerEndpointMiddleware>.Instance);

        HttpContext context = BuildContext("/swagger/orders/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("orders", body.RootElement.GetProperty("documentName").GetString());
    }

    private sealed class AlwaysFailingLoader : ISwaggerDocumentLoader
    {
        public Task<SwaggerLoadResult> LoadAsync(
            SwaggerEndpoint endpoint,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SwaggerLoadResult
            {
                Endpoint = endpoint,
                FailureStage = "http",
                HttpStatusCode = 503,
                ErrorMessage = "down",
            });
    }

    [Fact]
    public async Task UnavailableStatusCode_HonorsConfiguredOverride()
    {
        SwaggerEndpoint endpoint = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        AggregatedSwaggerEndpointMiddleware middleware = CreateMiddleware(
            new StubDiscoveryService(new SwaggerEndpointDiscoveryResult([endpoint], [])),
            new StubAggregator(_ => throw new InvalidOperationException("backend down")),
            configure: o => o.UnavailableStatusCode = StatusCodes.Status502BadGateway);

        HttpContext context = BuildContext("/swagger/orders/swagger.json");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
    }

    private static AggregatedSwaggerEndpointMiddleware CreateMiddleware(
        ISwaggerEndpointDiscoveryService discovery,
        ISwaggerAggregator aggregator,
        IAggregatedDocumentStore? store = null,
        RequestDelegate? next = null,
        Action<SwaggerAggregationDocumentEndpointOptions>? configure = null)
    {
        SwaggerDocumentCoordinator coordinator = new(
            discovery,
            aggregator,
            store ?? new InMemoryAggregatedDocumentStore(),
            TestDoubles.OptionsMonitor(),
            NullLogger<SwaggerDocumentCoordinator>.Instance);

        SwaggerAggregationDocumentEndpointOptions endpointOptions = new();
        configure?.Invoke(endpointOptions);

        IOptions<SwaggerAggregationDocumentEndpointOptions> options =
            Options.Create(endpointOptions);

        return new AggregatedSwaggerEndpointMiddleware(
            next ?? (_ => Task.CompletedTask),
            coordinator,
            options,
            NullLogger<AggregatedSwaggerEndpointMiddleware>.Instance);
    }

    private static DefaultHttpContext BuildContext(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }
}
