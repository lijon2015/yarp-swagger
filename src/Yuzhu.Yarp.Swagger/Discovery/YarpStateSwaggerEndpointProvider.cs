using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Reads Swagger endpoints from live YARP proxy state.
/// </summary>
public sealed class YarpStateSwaggerEndpointProvider : ISwaggerEndpointProvider
{
    private readonly IProxyStateLookup _proxyState;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options;
    private readonly ILogger<YarpStateSwaggerEndpointProvider> _logger;

    public YarpStateSwaggerEndpointProvider(
        IProxyStateLookup proxyState,
        IOptionsMonitor<SwaggerAggregationOptions> options,
        ILogger<YarpStateSwaggerEndpointProvider> logger)
    {
        _proxyState = proxyState;
        _options = options;
        _logger = logger;
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints()
    {
        var endpoints = new List<SwaggerEndpoint>();
        var defaultSwaggerPath = _options.CurrentValue.DefaultSwaggerPath;

        foreach (var cluster in _proxyState.GetClusters())
        {
            var metadata = cluster.Model.Config.Metadata;
            string? GetMetadataValue(string key) => metadata?.GetValueOrDefault(key);

            if (metadata == null || !SwaggerEndpointDiscoveryHelper.IsSwaggerEnabled(GetMetadataValue))
            {
                continue;
            }

            string? baseAddress = null;

            var availableDestination = cluster.DestinationsState?.AvailableDestinations.FirstOrDefault();
            if (availableDestination != null)
            {
                baseAddress = availableDestination.Model.Config.Address;
            }
            else
            {
                var configuredDestination = cluster.Model.Config.Destinations?.Values.FirstOrDefault();
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

            var endpoint = SwaggerEndpointDiscoveryHelper.CreateEndpoint(
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

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
    {
        return GetEndpoints()
            .Where(ep => SwaggerEndpointDiscoveryHelper.MatchesDocumentName(ep, documentName))
            .ToList();
    }
}
