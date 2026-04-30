using Microsoft.Extensions.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Discovery.AddressResolution;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class AddressResolverTests
{
    [Fact]
    public async Task ConfiguredResolver_NotApplicableForNonConfigurationCandidate()
    {
        YarpConfiguredDestinationAddressResolver resolver = new();
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders");

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.Same(SwaggerAddressResolution.NotApplicable, outcome);
    }

    [Fact]
    public async Task ConfiguredResolver_ResolvesFirstNonEmptyDestinationAddress()
    {
        IConfigurationSection clusterSection = BuildClusterSection(new Dictionary<string, string?>
        {
            ["Destinations:Default:Address"] = "https://orders.test",
        });

        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders", nativeCluster: clusterSection);
        YarpConfiguredDestinationAddressResolver resolver = new();

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.True(outcome.Resolved);
        Assert.Equal(new Uri("https://orders.test"), outcome.BaseAddress);
    }

    [Fact]
    public async Task ConfiguredResolver_ResolvesFallbackDestinationWhenDestinationsAreAbsent()
    {
        IConfigurationSection clusterSection = BuildClusterSection(new Dictionary<string, string?>
        {
            ["FallbackDestinations:Fallback:Address"] = "https://fallback-orders.test",
        });

        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders", nativeCluster: clusterSection);
        YarpConfiguredDestinationAddressResolver resolver = new();

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.True(outcome.Resolved);
        Assert.Equal(new Uri("https://fallback-orders.test"), outcome.BaseAddress);
    }

    [Fact]
    public async Task ConfiguredResolver_RejectsNonHttpAddress()
    {
        IConfigurationSection clusterSection = BuildClusterSection(new Dictionary<string, string?>
        {
            ["Destinations:Default:Address"] = "ftp://orders.test",
        });

        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders", nativeCluster: clusterSection);
        YarpConfiguredDestinationAddressResolver resolver = new();

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.False(outcome.Resolved);
        Assert.NotNull(outcome.SkippedReason);
    }

    [Fact]
    public async Task ConfiguredResolver_SkipsWhenNoDestinationsConfigured()
    {
        IConfigurationSection clusterSection = BuildClusterSection(new Dictionary<string, string?>
        {
            ["Metadata:Swagger:Enabled"] = "true",
        });

        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders", nativeCluster: clusterSection);
        YarpConfiguredDestinationAddressResolver resolver = new();

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.False(outcome.Resolved);
        Assert.NotNull(outcome.SkippedReason);
        Assert.Contains("Destinations", outcome.SkippedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeResolver_NotApplicableForNonClusterStateCandidate()
    {
        YarpRuntimeDestinationAddressResolver resolver = new();
        SwaggerClusterCandidate candidate = TestDoubles.CreateCandidate("orders");

        SwaggerAddressResolution outcome = await resolver.ResolveAsync(
            new SwaggerClusterDiscoveryContext(candidate, "/swagger/v1/swagger.json"));

        Assert.Same(SwaggerAddressResolution.NotApplicable, outcome);
    }

    private static IConfigurationSection BuildClusterSection(IDictionary<string, string?> values)
    {
        Dictionary<string, string?> nested = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string?> entry in values)
        {
            nested[$"ReverseProxy:Clusters:Cluster:{entry.Key}"] = entry.Value;
        }

        IConfigurationRoot root = new ConfigurationBuilder()
            .AddInMemoryCollection(nested)
            .Build();

        return root.GetSection("ReverseProxy:Clusters:Cluster");
    }
}
