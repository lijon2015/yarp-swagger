using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Reads Swagger endpoints directly from configuration.
/// </summary>
public sealed class ConfigBasedSwaggerEndpointProvider(
    IConfiguration configuration,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<ConfigBasedSwaggerEndpointProvider> logger) : ISwaggerEndpointProvider
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<ConfigBasedSwaggerEndpointProvider> _logger = logger;

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints()
    {
        List<SwaggerEndpoint> endpoints = [];
        string defaultSwaggerPath = _options.CurrentValue.DefaultSwaggerPath;

        IConfigurationSection clustersSection = _configuration.GetSection("ReverseProxy:Clusters");
        if (!clustersSection.Exists())
        {
            clustersSection = _configuration.GetSection("Yarp:Clusters");
        }

        if (!clustersSection.Exists())
        {
            _logger.LogWarning("No clusters configuration found in ReverseProxy:Clusters or Yarp:Clusters");
            return endpoints;
        }

        foreach (IConfigurationSection clusterSection in clustersSection.GetChildren())
        {
            string clusterId = clusterSection.Key;
            IConfigurationSection metadataSection = clusterSection.GetSection("Metadata");

            string? GetMetadataValue(string key)
            {
                return metadataSection[key];
            }

            if (!SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(GetMetadataValue))
            {
                continue;
            }

            string? baseAddress = GetBaseAddress(clusterSection);
            if (string.IsNullOrEmpty(baseAddress))
            {
                _logger.LogWarning(
                    "Cluster {ClusterId} has no destinations configured",
                    clusterId);
                continue;
            }

            SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
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

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName) => [.. GetEndpoints().Where(ep => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(ep, documentName))];

    private static string? GetBaseAddress(IConfigurationSection clusterSection)
    {
        IConfigurationSection destinationsSection = clusterSection.GetSection("Destinations");
        if (!destinationsSection.Exists())
        {
            return null;
        }

        IConfigurationSection? firstDestination = destinationsSection.GetChildren().FirstOrDefault();
        return firstDestination?["Address"];
    }
}
