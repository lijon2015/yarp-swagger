using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Merging;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class MergerTests
{
    [Fact]
    public void Merge_WithNoFailures_ProducesDocumentNamedAfterDocumentName()
    {
        DefaultSwaggerDocumentMerger merger = new(NullLogger<DefaultSwaggerDocumentMerger>.Instance);
        SwaggerLoadResult source = new()
        {
            Endpoint = TestDoubles.CreateEndpoint("orders", "orders"),
            Document = TestDoubles.CreateDocument("Orders Service"),
        };

        OpenApiDocument result = merger.Merge("orders", [source], new SwaggerMergeOptions());

        Assert.Equal("orders", result.Info.Title);
        Assert.Empty(result.Info.Description ?? string.Empty);
    }

    [Fact]
    public void Merge_PrefersInfoFromMetadataSourceEndpoint()
    {
        DefaultSwaggerDocumentMerger merger = new(NullLogger<DefaultSwaggerDocumentMerger>.Instance);

        OpenApiDocument metadataDoc = new()
        {
            Info = new OpenApiInfo { Title = "Authoritative", Version = "v2" },
            Paths = [],
        };
        OpenApiDocument otherDoc = new()
        {
            Info = new OpenApiInfo { Title = "Other", Version = "v1" },
            Paths = [],
        };

        OpenApiDocument result = merger.Merge(
            "orders",
            [
                new SwaggerLoadResult
                {
                    Endpoint = TestDoubles.CreateEndpoint("primary", "orders", isMetadataSource: true),
                    Document = metadataDoc,
                },
                new SwaggerLoadResult
                {
                    Endpoint = TestDoubles.CreateEndpoint("secondary", "orders"),
                    Document = otherDoc,
                },
            ],
            new SwaggerMergeOptions());

        Assert.Equal("Authoritative", result.Info.Title);
        Assert.Equal("v2", result.Info.Version);
    }

    [Fact]
    public void Merge_PathConflicts_KeepFirstDefinition()
    {
        DefaultSwaggerDocumentMerger merger = new(NullLogger<DefaultSwaggerDocumentMerger>.Instance);
        OpenApiDocument first = TestDoubles.CreateDocument("orders");
        first.Paths["/api/orders"] = new OpenApiPathItem { Description = "from first" };

        OpenApiDocument second = TestDoubles.CreateDocument("orders");
        second.Paths["/api/orders"] = new OpenApiPathItem { Description = "from second" };

        OpenApiDocument result = merger.Merge(
            "orders",
            [
                new SwaggerLoadResult
                {
                    Endpoint = TestDoubles.CreateEndpoint("primary", "orders"),
                    Document = first,
                },
                new SwaggerLoadResult
                {
                    Endpoint = TestDoubles.CreateEndpoint("secondary", "orders"),
                    Document = second,
                },
            ],
            new SwaggerMergeOptions());

        Assert.Equal("from first", ((OpenApiPathItem)result.Paths["/api/orders"]).Description);
    }

    [Fact]
    public void Merge_FailedServicesWarning_OnlyAddedWhenOptionSet()
    {
        DefaultSwaggerDocumentMerger merger = new(NullLogger<DefaultSwaggerDocumentMerger>.Instance);
        SwaggerLoadResult success = new()
        {
            Endpoint = TestDoubles.CreateEndpoint("orders", "orders"),
            Document = TestDoubles.CreateDocument("orders"),
        };
        SwaggerLoadResult failure = new()
        {
            Endpoint = TestDoubles.CreateEndpoint("billing", "orders"),
            ErrorMessage = "down",
            FailureStage = "http",
        };

        OpenApiDocument silent = merger.Merge(
            "orders",
            [success, failure],
            new SwaggerMergeOptions { IncludeFailedServicesWarning = false });
        Assert.True(string.IsNullOrEmpty(silent.Info.Description));

        OpenApiDocument loud = merger.Merge(
            "orders",
            [success, failure],
            new SwaggerMergeOptions { IncludeFailedServicesWarning = true });
        Assert.NotNull(loud.Info.Description);
        Assert.Contains("billing", loud.Info.Description, StringComparison.Ordinal);
    }
}
