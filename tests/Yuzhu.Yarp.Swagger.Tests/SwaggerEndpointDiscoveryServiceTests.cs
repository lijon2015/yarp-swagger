using Microsoft.Extensions.Logging.Abstractions;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerEndpointDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_WhenSwaggerNotEnabled_SkipsClusterAndEmitsDiagnostic()
    {
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "false",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([candidate])],
            resolvers: [new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://orders.test")))]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        Assert.Empty(result.Endpoints);
        SwaggerEndpointDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("orders", diagnostic.ClusterId);
        Assert.Equal(SwaggerDiagnosticStage.Metadata, diagnostic.Stage);
    }

    [Fact]
    public async Task DiscoverAsync_WhenAllResolversNotApplicable_EmitsAddressDiagnostic()
    {
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([candidate])],
            resolvers: [
                new StubAddressResolver(_ => SwaggerAddressResolution.NotApplicable),
                new StubAddressResolver(_ => SwaggerAddressResolution.Skipped("no destinations")),
            ]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        Assert.Empty(result.Endpoints);
        SwaggerEndpointDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(SwaggerDiagnosticStage.Address, diagnostic.Stage);
        Assert.Equal("no destinations", diagnostic.Message);
    }

    [Fact]
    public async Task DiscoverAsync_DeduplicatesByClusterIdWithFirstResolvedSourceWinning()
    {
        SwaggerClusterCandidate runtime = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "from-runtime",
            });
        SwaggerClusterCandidate config = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "from-config",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [
                new StubEndpointSource([runtime]),
                new StubEndpointSource([config]),
            ],
            resolvers: [new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://x.test")))]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        SwaggerEndpoint endpoint = Assert.Single(result.Endpoints);
        Assert.Equal("from-runtime", endpoint.DocumentName);

        // The duplicate-from-second-source diagnostic must be present so operators can see
        // why the second source was ignored.
        Assert.Contains(result.Diagnostics, d =>
            d.ClusterId == "orders"
            && d.Stage == SwaggerDiagnosticStage.Source
            && d.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DiscoverAsync_WhenFirstSourceCannotResolveAddress_TriesLaterDuplicateSource()
    {
        SwaggerClusterCandidate runtime = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "from-runtime",
            });
        SwaggerClusterCandidate config = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "from-config",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [
                new StubEndpointSource([runtime]),
                new StubEndpointSource([config]),
            ],
            resolvers: [
                new StubAddressResolver(context =>
                    string.Equals(context.Candidate.DocumentName, "from-runtime", StringComparison.Ordinal)
                        ? SwaggerAddressResolution.Skipped("runtime has no destinations")
                        : SwaggerAddressResolution.Resolve(new Uri("https://config.test"))),
            ]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        SwaggerEndpoint endpoint = Assert.Single(result.Endpoints);
        Assert.Equal("from-config", endpoint.DocumentName);
        Assert.Equal(new Uri("https://config.test"), endpoint.BaseAddress);
        Assert.Contains(result.Diagnostics, d =>
            d.ClusterId == "orders"
            && d.Stage == SwaggerDiagnosticStage.Address
            && d.Message == "runtime has no destinations");
        Assert.DoesNotContain(result.Diagnostics, d =>
            d.ClusterId == "orders"
            && d.Stage == SwaggerDiagnosticStage.Source
            && d.DocumentName == "from-config");
    }

    [Fact]
    public async Task DiscoverAsync_BuildsFullEndpointFromMetadata()
    {
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "orders-doc",
                [MetadataKeys.Path] = "/openapi/v3.json",
                [MetadataKeys.Prefix] = "/proxy-orders",
                [MetadataKeys.PathFilter] = "^/api/.*",
                [MetadataKeys.AccessTokenClient] = "Identity",
                [MetadataKeys.IsMetadataSource] = "true",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([candidate])],
            resolvers: [new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://orders.test")))]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        SwaggerEndpoint endpoint = Assert.Single(result.Endpoints);
        Assert.Equal("orders", endpoint.ClusterId);
        Assert.Equal("orders-doc", endpoint.DocumentName);
        Assert.Equal(new Uri("https://orders.test"), endpoint.BaseAddress);
        Assert.Equal("/openapi/v3.json", endpoint.SwaggerPath);
        Assert.Equal("/proxy-orders", endpoint.PathPrefix);
        Assert.Equal("^/api/.*", endpoint.PathFilter);
        Assert.Equal("Identity", endpoint.AccessTokenClient);
        Assert.True(endpoint.IsMetadataSource);
    }

    [Fact]
    public async Task DiscoverAsync_WithDocumentName_FiltersEndpointsButKeepsAllDiagnostics()
    {
        SwaggerClusterCandidate matching = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "orders-doc",
            });
        SwaggerClusterCandidate other = TestDoubles.CreateCandidate(
            "billing",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
                [MetadataKeys.DocumentName] = "billing-doc",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([matching, other])],
            resolvers: [new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://x.test")))]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync("orders-doc");

        SwaggerEndpoint endpoint = Assert.Single(result.Endpoints);
        Assert.Equal("orders-doc", endpoint.DocumentName);
        Assert.Equal(2, result.Diagnostics.Count);
    }

    [Fact]
    public async Task DiscoverAsync_DocumentNameDefaultsToClusterIdWhenMetadataAbsent()
    {
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
            });

        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([candidate])],
            resolvers: [new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://x.test")))]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        Assert.Equal("orders", Assert.Single(result.Endpoints).DocumentName);
    }

    [Fact]
    public async Task DiscoverAsync_FirstResolverThatResolvesWins()
    {
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate(
            "orders",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MetadataKeys.Enabled] = "true",
            });

        bool secondResolverCalled = false;
        SwaggerEndpointDiscoveryService service = CreateService(
            sources: [new StubEndpointSource([candidate])],
            resolvers: [
                new StubAddressResolver(_ => SwaggerAddressResolution.Resolve(new Uri("https://first.test"))),
                new StubAddressResolver(_ =>
                {
                    secondResolverCalled = true;
                    return SwaggerAddressResolution.Resolve(new Uri("https://second.test"));
                }),
            ]);

        SwaggerEndpointDiscoveryResult result = await service.DiscoverAsync();

        Assert.Equal(new Uri("https://first.test"), Assert.Single(result.Endpoints).BaseAddress);
        Assert.False(secondResolverCalled);
    }

    private static SwaggerEndpointDiscoveryService CreateService(
        IEnumerable<ISwaggerEndpointSource> sources,
        IEnumerable<ISwaggerEndpointAddressResolver> resolvers,
        SwaggerAggregationOptions? options = null) =>
        new(
            sources,
            resolvers,
            TestDoubles.OptionsMonitor(options),
            NullLogger<SwaggerEndpointDiscoveryService>.Instance);
}
