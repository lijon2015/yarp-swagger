using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Reads Swagger endpoints directly from configuration.
/// </summary>
public sealed class ConfigBasedSwaggerEndpointProvider : ISwaggerEndpointProvider
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options;
    private readonly ILogger<ConfigBasedSwaggerEndpointProvider> _logger;

    public ConfigBasedSwaggerEndpointProvider(
        IConfiguration configuration,
        IOptionsMonitor<SwaggerAggregationOptions> options,
        ILogger<ConfigBasedSwaggerEndpointProvider> logger)
    {
        _configuration = configuration;
        _options = options;
        _logger = logger;
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints()
    {
        var endpoints = new List<SwaggerEndpoint>();
        var defaultSwaggerPath = _options.CurrentValue.DefaultSwaggerPath;

        var clustersSection = _configuration.GetSection("ReverseProxy:Clusters");
        if (!clustersSection.Exists())
        {
            clustersSection = _configuration.GetSection("Yarp:Clusters");
        }

        if (!clustersSection.Exists())
        {
            _logger.LogWarning("No clusters configuration found in ReverseProxy:Clusters or Yarp:Clusters");
            return endpoints;
        }

        foreach (var clusterSection in clustersSection.GetChildren())
        {
            var clusterId = clusterSection.Key;
            var metadataSection = clusterSection.GetSection("Metadata");

            string? GetMetadataValue(string key) => metadataSection[key];

            if (!SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(GetMetadataValue))
            {
                continue;
            }

            var baseAddress = GetBaseAddress(clusterSection);
            if (string.IsNullOrEmpty(baseAddress))
            {
                _logger.LogWarning(
                    "Cluster {ClusterId} has no destinations configured",
                    clusterId);
                continue;
            }

            var endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
                clusterId,
                baseAddress,
                GetMetadataValue,
                defaultSwaggerPath,
                _logger);

            if (endpoint != null)
            {
                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
    {
        return GetEndpoints()
            .Where(ep => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(ep, documentName))
            .ToList();
    }

    private static string? GetBaseAddress(IConfigurationSection clusterSection)
    {
        var destinationsSection = clusterSection.GetSection("Destinations");
        if (!destinationsSection.Exists())
        {
            return null;
        }

        var firstDestination = destinationsSection.GetChildren().FirstOrDefault();
        return firstDestination?["Address"];
    }
}
