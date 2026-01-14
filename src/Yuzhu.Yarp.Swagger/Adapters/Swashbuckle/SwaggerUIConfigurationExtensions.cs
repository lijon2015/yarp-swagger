using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerUI;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Swagger UI 配置扩展方法
/// </summary>
public static class SwaggerUIConfigurationExtensions
{
    /// <summary>
    /// 配置 Swagger UI 端点（基于聚合配置）
    /// </summary>
    public static void ConfigureAggregatedEndpoints(
        this SwaggerUIOptions options,
        IServiceProvider serviceProvider)
    {
        var urls = new List<UrlDescriptor>();

        // 直接从 IConfiguration 读取 YARP 的 Clusters 配置
        // 这在应用启动时就可用，不依赖 YARP 运行时状态
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // 尝试从多个常见配置路径读取
        var clustersSection = configuration.GetSection("ReverseProxy:Clusters");
        if (!clustersSection.Exists())
        {
            clustersSection = configuration.GetSection("Yarp:Clusters");
        }

        if (clustersSection.Exists())
        {
            foreach (var clusterSection in clustersSection.GetChildren())
            {
                var clusterId = clusterSection.Key;
                var metadataSection = clusterSection.GetSection("Metadata");

                if (metadataSection.Exists())
                {
                    var enabled = metadataSection[MetadataKeys.Enabled];
                    if (string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        // 使用 DocumentName 或 ClusterId 作为文档名称
                        var docName = metadataSection[MetadataKeys.DocumentName] ?? clusterId;
                        if (!string.IsNullOrEmpty(docName))
                        {
                            urls.Add(new UrlDescriptor
                            {
                                Url = $"/swagger/{docName}/swagger.json",
                                Name = docName
                            });
                        }
                    }
                }
            }
        }

        // 去重
        urls = urls.DistinctBy(u => u.Name).ToList();

        // 如果没有发现任何端点，添加占位符
        if (urls.Count == 0)
        {
            urls.Add(new UrlDescriptor
            {
                Url = "/swagger/v1/swagger.json",
                Name = "API (Loading...)"
            });
        }

        options.ConfigObject.Urls = urls;
    }
}
