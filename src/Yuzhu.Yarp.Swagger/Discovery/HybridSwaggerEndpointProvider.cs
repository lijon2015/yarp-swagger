using Microsoft.Extensions.Logging;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// Endpoint provider that prefers live YARP state and falls back to configuration.
/// </summary>
public sealed class HybridSwaggerEndpointProvider : ISwaggerEndpointProvider
{
    private readonly ISwaggerEndpointProvider _yarpStateProvider;
    private readonly ISwaggerEndpointProvider _configProvider;
    private readonly ILogger<HybridSwaggerEndpointProvider> _logger;

    public HybridSwaggerEndpointProvider(
        YarpStateSwaggerEndpointProvider yarpStateProvider,
        ConfigBasedSwaggerEndpointProvider configProvider,
        ILogger<HybridSwaggerEndpointProvider> logger)
        : this(
            yarpStateProvider,
            (ISwaggerEndpointProvider)configProvider,
            logger)
    {
    }

    internal HybridSwaggerEndpointProvider(
        ISwaggerEndpointProvider yarpStateProvider,
        ISwaggerEndpointProvider configProvider,
        ILogger<HybridSwaggerEndpointProvider> logger)
    {
        _yarpStateProvider = yarpStateProvider;
        _configProvider = configProvider;
        _logger = logger;
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints()
    {
        IReadOnlyList<SwaggerEndpoint> stateEndpoints = _yarpStateProvider.GetEndpoints();
        IReadOnlyList<SwaggerEndpoint> configEndpoints = _configProvider.GetEndpoints();

        return MergeEndpoints(stateEndpoints, configEndpoints);
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
    {
        IReadOnlyList<SwaggerEndpoint> stateEndpoints = _yarpStateProvider.GetEndpoints(documentName);
        IReadOnlyList<SwaggerEndpoint> configEndpoints = _configProvider.GetEndpoints(documentName);

        return MergeEndpoints(stateEndpoints, configEndpoints, documentName);
    }

    private IReadOnlyList<SwaggerEndpoint> MergeEndpoints(
        IReadOnlyList<SwaggerEndpoint> preferredEndpoints,
        IReadOnlyList<SwaggerEndpoint> fallbackEndpoints,
        string? documentName = null)
    {
        if (preferredEndpoints.Count == 0 && fallbackEndpoints.Count == 0)
        {
            if (documentName == null)
            {
                _logger.LogDebug("No swagger endpoints available from live YARP state or configuration");
            }
            else
            {
                _logger.LogDebug(
                    "Document '{DocumentName}' not available from live YARP state or configuration",
                    documentName);
            }

            return [];
        }

        if (preferredEndpoints.Count == 0)
        {
            if (documentName == null)
            {
                _logger.LogDebug("No swagger endpoints available from live YARP state, falling back to configuration");
            }
            else
            {
                _logger.LogDebug(
                    "Document '{DocumentName}' not available from live YARP state, falling back to configuration",
                    documentName);
            }

            return fallbackEndpoints;
        }

        if (fallbackEndpoints.Count == 0)
        {
            return preferredEndpoints;
        }

        List<SwaggerEndpoint> mergedEndpoints = new List<SwaggerEndpoint>(preferredEndpoints.Count + fallbackEndpoints.Count);
        HashSet<string> knownEndpointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (SwaggerEndpoint endpoint in preferredEndpoints)
        {
            mergedEndpoints.Add(endpoint);
            _ = knownEndpointIds.Add(SwaggerEndpointDiscoveryHelper.GetEndpointIdentity(endpoint));
        }

        foreach (SwaggerEndpoint endpoint in fallbackEndpoints)
        {
            if (knownEndpointIds.Add(SwaggerEndpointDiscoveryHelper.GetEndpointIdentity(endpoint)))
            {
                mergedEndpoints.Add(endpoint);
            }
        }

        return mergedEndpoints;
    }
}
