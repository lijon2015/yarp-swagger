using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// 基于 YARP 运行时状态的 Swagger 端点提供者
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

            // 检查是否启用 Swagger
            if (metadata == null ||
                !metadata.TryGetValue(MetadataKeys.Enabled, out var enabled) ||
                !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 优先获取健康的目标地址，如果没有则获取任意配置的目标地址
            string? baseAddress = null;

            // 首先尝试获取可用的目标
            var availableDestination = cluster.DestinationsState?.AvailableDestinations
                .FirstOrDefault();

            if (availableDestination != null)
            {
                baseAddress = availableDestination.Model.Config.Address;
            }
            else
            {
                // 如果没有可用目标，从配置中获取第一个目标地址
                var configuredDestination = cluster.Model.Config.Destinations?
                    .Values.FirstOrDefault();

                if (configuredDestination != null)
                {
                    baseAddress = configuredDestination.Address;
                    _logger.LogWarning(
                        "Cluster {ClusterId} has no available destinations, using configured address: {Address}",
                        cluster.ClusterId, baseAddress);
                }
                else
                {
                    _logger.LogWarning(
                        "Cluster {ClusterId} has no destinations configured",
                        cluster.ClusterId);
                    continue;
                }
            }

            if (string.IsNullOrEmpty(baseAddress))
            {
                continue;
            }

            try
            {
                var endpoint = new SwaggerEndpoint
                {
                    ClusterId = cluster.ClusterId,
                    BaseAddress = new Uri(baseAddress),
                    SwaggerPath = metadata.GetValueOrDefault(MetadataKeys.Path, defaultSwaggerPath),
                    PathPrefix = metadata.GetValueOrDefault(MetadataKeys.Prefix),
                    PathFilter = metadata.GetValueOrDefault(MetadataKeys.PathFilter),
                    AccessTokenClient = metadata.GetValueOrDefault(MetadataKeys.AccessTokenClient),
                    OnlyPublishedPaths = metadata.TryGetValue(MetadataKeys.OnlyPublishedPaths, out var onlyPublished)
                        && string.Equals(onlyPublished, "true", StringComparison.OrdinalIgnoreCase),
                    IsMetadataSource = metadata.TryGetValue(MetadataKeys.IsMetadataSource, out var isMeta)
                        && string.Equals(isMeta, "true", StringComparison.OrdinalIgnoreCase),
                    DocumentName = metadata.GetValueOrDefault(MetadataKeys.DocumentName)
                };

                endpoints.Add(endpoint);

                _logger.LogDebug(
                    "Discovered Swagger endpoint for cluster {ClusterId}: {SwaggerUrl}",
                    cluster.ClusterId, endpoint.SwaggerUrl);
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(ex,
                    "Invalid base address for cluster {ClusterId}: {Address}",
                    cluster.ClusterId, baseAddress);
            }
        }

        return endpoints;
    }

    public IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName)
    {
        return GetEndpoints()
            .Where(ep =>
            {
                // 文档名称匹配逻辑：
                // 1. 如果端点指定了 DocumentName，则按 DocumentName 匹配
                // 2. 如果端点没有指定 DocumentName，则按 ClusterId 匹配
                var effectiveDocumentName = ep.DocumentName ?? ep.ClusterId;
                return string.Equals(effectiveDocumentName, documentName, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }
}
