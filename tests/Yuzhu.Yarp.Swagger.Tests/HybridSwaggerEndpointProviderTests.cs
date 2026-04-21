using Microsoft.Extensions.Logging.Abstractions;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Discovery;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class HybridSwaggerEndpointProviderTests
{
    [Fact]
    public void GetEndpoints_MergesConfigEndpointsMissingFromLiveState()
    {
        var sharedStateEndpoint = CreateEndpoint("orders-cluster", "orders", "https://live-orders.test");
        var stateOnlyEndpoint = CreateEndpoint("inventory-cluster", "inventory", "https://live-inventory.test");
        var sharedConfigEndpoint = CreateEndpoint("orders-cluster", "orders", "https://config-orders.test");
        var configOnlyEndpoint = CreateEndpoint("billing-cluster", "billing", "https://config-billing.test");

        var provider = new HybridSwaggerEndpointProvider(
            new StubSwaggerEndpointProvider([sharedStateEndpoint, stateOnlyEndpoint]),
            new StubSwaggerEndpointProvider([sharedConfigEndpoint, configOnlyEndpoint]),
            NullLogger<HybridSwaggerEndpointProvider>.Instance);

        var endpoints = provider.GetEndpoints();

        Assert.Collection(
            endpoints,
            endpoint => Assert.Equal("orders-cluster", endpoint.ClusterId),
            endpoint => Assert.Equal("inventory-cluster", endpoint.ClusterId),
            endpoint => Assert.Equal("billing-cluster", endpoint.ClusterId));
    }

    [Fact]
    public void GetEndpoints_ForDocumentName_MergesMatchingFallbackEndpoints()
    {
        var stateEndpoint = CreateEndpoint("orders-cluster", "shared", "https://live-orders.test");
        var fallbackEndpoint = CreateEndpoint("billing-cluster", "shared", "https://config-billing.test");

        var provider = new HybridSwaggerEndpointProvider(
            new StubSwaggerEndpointProvider([stateEndpoint]),
            new StubSwaggerEndpointProvider([fallbackEndpoint]),
            NullLogger<HybridSwaggerEndpointProvider>.Instance);

        var endpoints = provider.GetEndpoints("shared");

        Assert.Collection(
            endpoints,
            endpoint => Assert.Equal("orders-cluster", endpoint.ClusterId),
            endpoint => Assert.Equal("billing-cluster", endpoint.ClusterId));
    }

    [Fact]
    public void GetEndpoints_WhenLiveStateIsEmpty_FallsBackToConfiguration()
    {
        var configEndpoint = CreateEndpoint("reports-cluster", "reports", "https://config-reports.test");

        var provider = new HybridSwaggerEndpointProvider(
            new StubSwaggerEndpointProvider([]),
            new StubSwaggerEndpointProvider([configEndpoint]),
            NullLogger<HybridSwaggerEndpointProvider>.Instance);

        var endpoints = provider.GetEndpoints();

        var endpoint = Assert.Single(endpoints);
        Assert.Equal("reports-cluster", endpoint.ClusterId);
        Assert.Equal(new Uri("https://config-reports.test"), endpoint.BaseAddress);
    }

    private static SwaggerEndpoint CreateEndpoint(string clusterId, string documentName, string baseAddress)
    {
        return new SwaggerEndpoint
        {
            ClusterId = clusterId,
            BaseAddress = new Uri(baseAddress),
            SwaggerPath = "/swagger/v1/swagger.json",
            DocumentName = documentName
        };
    }

    private sealed class StubSwaggerEndpointProvider(IReadOnlyList<SwaggerEndpoint> endpoints) : ISwaggerEndpointProvider
    {
        public IReadOnlyList<SwaggerEndpoint> GetEndpoints() => endpoints;

        public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
        {
            return endpoints
                .Where(endpoint => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, documentName))
                .ToList();
        }
    }
}
