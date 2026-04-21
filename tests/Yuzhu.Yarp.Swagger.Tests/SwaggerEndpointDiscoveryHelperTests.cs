using Microsoft.Extensions.Logging.Abstractions;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerEndpointDiscoveryHelperTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("yes", false)]
    [InlineData("1", false)]
    public void IsTrue_OnlyMatchesLiteralTrueCaseInsensitively(string? value, bool expected) => Assert.Equal(expected, SwaggerEndpointDiscoveryHelper.IsTrue(value));

    [Fact]
    public void IsSwaggerEnabled_ReadsEnabledMetadataKey()
    {
        Assert.True(SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(key =>
            key == MetadataKeys.Enabled ? "true" : null));

        Assert.False(SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(key =>
            key == MetadataKeys.Enabled ? "false" : null));

        Assert.False(SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(_ => null));
    }

    [Fact]
    public void MatchesDocumentName_UsesDocumentNameWhenSet()
    {
        SwaggerEndpoint endpoint = CreateMinimalEndpoint("cluster-a", documentName: "orders");

        Assert.True(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "orders"));
        Assert.True(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "ORDERS"));
        Assert.False(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "cluster-a"));
    }

    [Fact]
    public void MatchesDocumentName_FallsBackToClusterIdWhenDocumentNameIsNull()
    {
        SwaggerEndpoint endpoint = CreateMinimalEndpoint("cluster-a", documentName: null);

        Assert.True(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "cluster-a"));
        Assert.True(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "CLUSTER-A"));
        Assert.False(SwaggerEndpointDiscoveryHelper.MatchesDocumentName(endpoint, "orders"));
    }

    [Fact]
    public void GetEffectiveDocumentName_PrefersDocumentNameOverClusterId()
    {
        Assert.Equal(
            "orders",
            SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName(
                CreateMinimalEndpoint("cluster-a", "orders")));

        Assert.Equal(
            "cluster-a",
            SwaggerEndpointDiscoveryHelper.GetEffectiveDocumentName(
                CreateMinimalEndpoint("cluster-a", documentName: null)));
    }

    [Fact]
    public void GetEndpointIdentity_CombinesClusterIdAndDocumentNameWithSeparator()
    {
        SwaggerEndpoint endpoint = CreateMinimalEndpoint("cluster-a", "orders");

        Assert.Equal("cluster-a|orders", SwaggerEndpointDiscoveryHelper.GetEndpointIdentity(endpoint));
    }

    [Fact]
    public void GetEndpointIdentity_UsesClusterIdOnBothSidesWhenDocumentNameMissing()
    {
        SwaggerEndpoint endpoint = CreateMinimalEndpoint("cluster-a", documentName: null);

        Assert.Equal("cluster-a|cluster-a", SwaggerEndpointDiscoveryHelper.GetEndpointIdentity(endpoint));
    }

    [Fact]
    public void CreateEndpoint_PopulatesAllSupportedMetadataFields()
    {
        Dictionary<string, string?> metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [MetadataKeys.Path] = "/custom/swagger.json",
            [MetadataKeys.Prefix] = "/api-v1",
            [MetadataKeys.PathFilter] = "^/api/.*",
            [MetadataKeys.AccessTokenClient] = "oauth-client",
            [MetadataKeys.IsMetadataSource] = "true",
            [MetadataKeys.DocumentName] = "orders",
        };

        SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
            "orders-cluster",
            "https://orders.test",
            key => metadata.GetValueOrDefault(key),
            "/swagger/v1/swagger.json",
            NullLogger.Instance);

        Assert.NotNull(endpoint);
        Assert.Equal("orders-cluster", endpoint.ClusterId);
        Assert.Equal(new Uri("https://orders.test"), endpoint.BaseAddress);
        Assert.Equal("/custom/swagger.json", endpoint.SwaggerPath);
        Assert.Equal("/api-v1", endpoint.PathPrefix);
        Assert.Equal("^/api/.*", endpoint.PathFilter);
        Assert.Equal("oauth-client", endpoint.AccessTokenClient);
        Assert.True(endpoint.IsMetadataSource);
        Assert.Equal("orders", endpoint.DocumentName);
    }

    /// <summary>
    /// Regression guard for v2.0.0: the removed <c>Swagger:OnlyPublishedPaths</c> key must
    /// never influence endpoint creation. If someone re-introduces a property with that
    /// name, this test will need to be revisited.
    /// </summary>
    [Fact]
    public void CreateEndpoint_IgnoresRemovedOnlyPublishedPathsMetadataKey()
    {
        List<string> readKeys = [];
        Dictionary<string, string?> metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Swagger:OnlyPublishedPaths"] = "true",
        };

        SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
            "orders-cluster",
            "https://orders.test",
            key =>
            {
                readKeys.Add(key);
                return metadata.GetValueOrDefault(key);
            },
            "/swagger/v1/swagger.json",
            NullLogger.Instance);

        Assert.NotNull(endpoint);
        Assert.DoesNotContain("Swagger:OnlyPublishedPaths", readKeys);
        Assert.DoesNotContain(
            typeof(SwaggerEndpoint).GetProperties(),
            property => property.Name == "OnlyPublishedPaths");
    }

    [Fact]
    public void CreateEndpoint_UsesDefaultSwaggerPathWhenPathMetadataMissing()
    {
        SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
            "orders-cluster",
            "https://orders.test",
            _ => null,
            "/swagger/v1/swagger.json",
            NullLogger.Instance);

        Assert.NotNull(endpoint);
        Assert.Equal("/swagger/v1/swagger.json", endpoint.SwaggerPath);
        Assert.False(endpoint.IsMetadataSource);
        Assert.Null(endpoint.DocumentName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateEndpoint_ReturnsNullForEmptyOrWhitespaceBaseAddress(string? baseAddress)
    {
        SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
            "orders-cluster",
            baseAddress,
            _ => null,
            "/swagger/v1/swagger.json",
            NullLogger.Instance);

        Assert.Null(endpoint);
    }

    [Fact]
    public void CreateEndpoint_ReturnsNullForInvalidBaseAddress()
    {
        SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
            "orders-cluster",
            "not a url",
            _ => null,
            "/swagger/v1/swagger.json",
            NullLogger.Instance);

        Assert.Null(endpoint);
    }

    private static SwaggerEndpoint CreateMinimalEndpoint(string clusterId, string? documentName)
    {
        return new SwaggerEndpoint
        {
            ClusterId = clusterId,
            BaseAddress = new Uri("https://example.test"),
            SwaggerPath = "/swagger/v1/swagger.json",
            DocumentName = documentName,
        };
    }
}
