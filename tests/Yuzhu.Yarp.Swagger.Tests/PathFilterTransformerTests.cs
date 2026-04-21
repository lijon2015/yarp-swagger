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
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var document = CreateDocumentWithPaths("/api/users", "/internal/health");

        var result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: null));

        Assert.Same(document, result);
        Assert.Equal(2, result.Paths.Count);
    }

    [Fact]
    public async Task TransformAsync_KeepsMatchingPathsAndDropsOthers()
    {
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var document = CreateDocumentWithPaths("/api/users", "/api/orders", "/internal/health");

        var result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: "^/api/.*"));

        Assert.Equal(2, result.Paths.Count);
        Assert.Contains("/api/users", result.Paths.Keys);
        Assert.Contains("/api/orders", result.Paths.Keys);
        Assert.DoesNotContain("/internal/health", result.Paths.Keys);
    }

    /// <summary>
    /// Migration proof for the v2.0.0 removal of <c>Swagger:OnlyPublishedPaths</c>: the
    /// documented replacement is to constrain surfaced paths with a regex via
    /// <c>Swagger:PathFilter</c>. This test pins that the replacement path actually works.
    /// </summary>
    [Fact]
    public async Task TransformAsync_PublicPathsRegex_IsValidMigrationFromOnlyPublishedPaths()
    {
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var document = CreateDocumentWithPaths(
            "/api/public/orders",
            "/api/public/invoices",
            "/api/internal/debug",
            "/api/admin/users");

        var result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: "^/api/public/.*"));

        Assert.Equal(2, result.Paths.Count);
        Assert.All(
            result.Paths.Keys,
            path => Assert.StartsWith("/api/public/", path));
    }

    [Fact]
    public async Task TransformAsync_InvalidRegex_KeepsDocumentUnchanged()
    {
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var document = CreateDocumentWithPaths("/api/users");

        var result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: "[["));

        Assert.Same(document, result);
        Assert.Single(result.Paths);
    }

    [Fact]
    public async Task TransformAsync_PatternExceedingMaxLength_KeepsDocumentUnchanged()
    {
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var document = CreateDocumentWithPaths("/api/users");
        var oversizedPattern = new string('a', SwaggerConstants.MaxPathFilterLength + 1);

        var result = await transformer.TransformAsync(
            document,
            BuildContext(pathFilter: oversizedPattern));

        Assert.Same(document, result);
        Assert.Single(result.Paths);
    }

    [Fact]
    public async Task TransformAsync_CompiledRegexIsCachedBetweenCalls()
    {
        var transformer = new PathFilterTransformer(NullLogger<PathFilterTransformer>.Instance);
        var context = BuildContext(pathFilter: "^/api/.*");

        var first = await transformer.TransformAsync(
            CreateDocumentWithPaths("/api/one", "/other/two"),
            context);
        var second = await transformer.TransformAsync(
            CreateDocumentWithPaths("/api/three", "/other/four"),
            context);

        Assert.Single(first.Paths);
        Assert.Contains("/api/one", first.Paths.Keys);

        Assert.Single(second.Paths);
        Assert.Contains("/api/three", second.Paths.Keys);
    }

    private static OpenApiDocument CreateDocumentWithPaths(params string[] paths)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "test", Version = "v1" },
            Paths = new OpenApiPaths(),
        };

        foreach (var path in paths)
        {
            document.Paths[path] = new OpenApiPathItem();
        }

        return document;
    }

    private static TransformContext BuildContext(string? pathFilter)
    {
        return new TransformContext
        {
            ClusterId = "test-cluster",
            Endpoint = new SwaggerEndpoint
            {
                ClusterId = "test-cluster",
                BaseAddress = new Uri("https://example.test"),
                SwaggerPath = "/swagger/v1/swagger.json",
                PathFilter = pathFilter,
            },
        };
    }
}
