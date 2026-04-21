using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Reads Swagger endpoints from live YARP proxy state.
/// </summary>
public sealed class YarpStateSwaggerEndpointProvider(
    IProxyStateLookup proxyState,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<YarpStateSwaggerEndpointProvider> logger) : ISwaggerEndpointProvider
{
    private readonly IProxyStateLookup _proxyState = proxyState;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<YarpStateSwaggerEndpointProvider> _logger = logger;

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints()
    {
        List<SwaggerEndpoint> endpoints = [];
        string defaultSwaggerPath = _options.CurrentValue.DefaultSwaggerPath;

        foreach (ClusterState cluster in _proxyState.GetClusters())
        {
            IReadOnlyDictionary<string, string>? metadata = cluster.Model.Config.Metadata;
            string? GetMetadataValue(string key)
            {
                return metadata?.GetValueOrDefault(key);
            }

            if (metadata == null || !SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(GetMetadataValue))
            {
                continue;
            }

            string? baseAddress = null;

            DestinationState? availableDestination = cluster.DestinationsState?.AvailableDestinations.FirstOrDefault();
            if (availableDestination != null)
            {
                baseAddress = availableDestination.Model.Config.Address;
            }
            else
            {
                DestinationConfig? configuredDestination = cluster.Model.Config.Destinations?.Values.FirstOrDefault();
                if (configuredDestination != null)
                {
                    _logger.LogWarning(
                        "Cluster {ClusterId} has no available destinations, using configured address: {Address}",
                        cluster.ClusterId,
                        configuredDestination.Address);

                    baseAddress = configuredDestination.Address;
                }
            }

            if (string.IsNullOrEmpty(baseAddress))
            {
                _logger.LogWarning(
                    "Cluster {ClusterId} has no destinations configured",
                    cluster.ClusterId);
                continue;
            }

            SwaggerEndpoint? endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
                cluster.ClusterId,
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
}
