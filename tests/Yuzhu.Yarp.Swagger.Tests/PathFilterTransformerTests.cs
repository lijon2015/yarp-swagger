using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Transforming;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class PathFilterTransformerTests
{
    [Fact]
    public async Task TransformAsync_WithoutFilter_ReturnsDocumentUnchanged()
    {
        PathFilterTransformer transformer = new(NullLogger<PathFilterTransformer>.Instance);
        OpenApiDocument document = CreateDocumentWithPaths("/api/users", "/internal/health");

        OpenApiDocument result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: null));

        Assert.Same(document, result);
        Assert.Equal(2, result.Paths.Count);
    }

    [Fact]
    public async Task TransformAsync_KeepsMatchingPathsAndDropsOthers()
    {
        PathFilterTransformer transformer = new(NullLogger<PathFilterTransformer>.Instance);
        OpenApiDocument document = CreateDocumentWithPaths("/api/users", "/api/orders", "/internal/health");

        OpenApiDocument result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: "^/api/.*"));

        Assert.Equal(2, result.Paths.Count);
        Assert.Contains("/api/users", result.Paths.Keys);
        Assert.Contains("/api/orders", result.Paths.Keys);
        Assert.DoesNotContain("/internal/health", result.Paths.Keys);
    }

    [Fact]
    public async Task TransformAsync_InvalidRegex_KeepsDocumentUnchanged()
    {
        PathFilterTransformer transformer = new(NullLogger<PathFilterTransformer>.Instance);
        OpenApiDocument document = CreateDocumentWithPaths("/api/users");

        OpenApiDocument result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: "[["));

        Assert.Same(document, result);
        _ = Assert.Single(result.Paths);
    }

    [Fact]
    public async Task TransformAsync_PatternExceedingMaxLength_KeepsDocumentUnchanged()
    {
        PathFilterTransformer transformer = new(NullLogger<PathFilterTransformer>.Instance);
        OpenApiDocument document = CreateDocumentWithPaths("/api/users");
        string oversized = new('a', SwaggerConstants.MaxPathFilterLength + 1);

        OpenApiDocument result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: oversized));

        Assert.Same(document, result);
        _ = Assert.Single(result.Paths);
    }

    [Fact]
    public async Task TransformAsync_CompiledRegexIsCachedBetweenCalls()
    {
        PathFilterTransformer transformer = new(NullLogger<PathFilterTransformer>.Instance);
        TransformContext context = BuildContext(pathFilter: "^/api/.*");

        OpenApiDocument first = await transformer.TransformAsync(
            CreateDocumentWithPaths("/api/one", "/other/two"),
            context);
        OpenApiDocument second = await transformer.TransformAsync(
            CreateDocumentWithPaths("/api/three", "/other/four"),
            context);

        _ = Assert.Single(first.Paths);
        Assert.Contains("/api/one", first.Paths.Keys);

        _ = Assert.Single(second.Paths);
        Assert.Contains("/api/three", second.Paths.Keys);
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

    private static TransformContext BuildContext(string? pathFilter) =>
        new()
        {
            ClusterId = "test-cluster",
            Endpoint = TestDoubles.CreateEndpoint("test-cluster", pathFilter: pathFilter),
        };
}
