using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

/// <summary>
/// 基于 IConfiguration 的 Swagger 端点提供者
/// 直接从配置文件读取集群信息，不依赖 YARP 运行时状态
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

        // 尝试从多个常见配置路径读取
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

            // 检查是否启用 Swagger
            var enabled = metadataSection[MetadataKeys.Enabled];
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 获取目标地址
            string? baseAddress = null;
            var destinationsSection = clusterSection.GetSection("Destinations");
            if (destinationsSection.Exists())
            {
                // 获取第一个 destination 的地址
                var firstDestination = destinationsSection.GetChildren().FirstOrDefault();
                if (firstDestination != null)
                {
                    baseAddress = firstDestination["Address"];
                }
            }

            if (string.IsNullOrEmpty(baseAddress))
            {
                _logger.LogWarning(
                    "Cluster {ClusterId} has no destinations configured",
                    clusterId);
                continue;
            }

            try
            {
                var endpoint = new SwaggerEndpoint
                {
                    ClusterId = clusterId,
                    BaseAddress = new Uri(baseAddress),
                    SwaggerPath = metadataSection[MetadataKeys.Path] ?? defaultSwaggerPath,
                    PathPrefix = metadataSection[MetadataKeys.Prefix],
                    PathFilter = metadataSection[MetadataKeys.PathFilter],
                    AccessTokenClient = metadataSection[MetadataKeys.AccessTokenClient],
                    OnlyPublishedPaths = string.Equals(
                        metadataSection[MetadataKeys.OnlyPublishedPaths],
                        "true",
                        StringComparison.OrdinalIgnoreCase),
                    IsMetadataSource = string.Equals(
                        metadataSection[MetadataKeys.IsMetadataSource],
                        "true",
                        StringComparison.OrdinalIgnoreCase),
                    DocumentName = metadataSection[MetadataKeys.DocumentName]
                };

                endpoints.Add(endpoint);

                _logger.LogDebug(
                    "Discovered Swagger endpoint for cluster {ClusterId}: {SwaggerUrl}",
                    clusterId, endpoint.SwaggerUrl);
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(ex,
                    "Invalid base address for cluster {ClusterId}: {Address}",
                    clusterId, baseAddress);
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
