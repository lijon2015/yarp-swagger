using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Transforming;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class PathPrefixTransformerTests
{
    [Fact]
    public async Task TransformAsync_WithoutPrefix_ReturnsDocumentUnchanged()
    {
        PathPrefixTransformer transformer = new();
        OpenApiDocument document = CreateDocumentWithPaths("/api/orders");

        OpenApiDocument result = await transformer.TransformAsync(document, BuildContext(prefix: null));

        Assert.Same(document, result);
        Assert.Contains("/api/orders", result.Paths.Keys);
    }

    [Fact]
    public async Task TransformAsync_PrependsPrefixToEveryPath()
    {
        PathPrefixTransformer transformer = new();
        OpenApiDocument document = CreateDocumentWithPaths("/api/orders", "/health");

        OpenApiDocument result = await transformer.TransformAsync(document, BuildContext(prefix: "/proxy-orders"));

        Assert.Contains("/proxy-orders/api/orders", result.Paths.Keys);
        Assert.Contains("/proxy-orders/health", result.Paths.Keys);
    }

    [Fact]
    public async Task TransformAsync_NormalizesTrailingSlashOnPrefix()
    {
        PathPrefixTransformer transformer = new();
        OpenApiDocument document = CreateDocumentWithPaths("/api/orders");

        OpenApiDocument result = await transformer.TransformAsync(document, BuildContext(prefix: "/proxy-orders/"));

        Assert.Contains("/proxy-orders/api/orders", result.Paths.Keys);
    }

    private static OpenApiDocument CreateDocumentWithPaths(params string[] paths)
    {
        OpenApiDocument document = new()
        {
            Info = new OpenApiInfo { Title = "test", Version = "v1" },
            Paths = [],
        };

        foreach (string path in paths)
        {
            document.Paths[path] = new OpenApiPathItem();
        }

        return document;
    }

    private static TransformContext BuildContext(string? prefix) =>
        new()
        {
            ClusterId = "test-cluster",
            Endpoint = TestDoubles.CreateEndpoint("test-cluster", pathPrefix: prefix),
        };
}
