using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Background;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Loading;
using Yuzhu.Yarp.Swagger.Merging;
using Yuzhu.Yarp.Swagger.Storage;
using Yuzhu.Yarp.Swagger.Telemetry;
using Yuzhu.Yarp.Swagger.Transforming;

namespace Yuzhu.Yarp.Swagger.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Swagger 聚合功能
    /// </summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        var services = builder.Services;

        // 配置选项
        services.AddOptions<SwaggerAggregationOptions>()
            .BindConfiguration(SwaggerAggregationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 添加具名 HttpClient（用于 Swagger 文档加载）
        services.AddHttpClient(SwaggerConstants.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10
        });

        // 核心服务
        // 使用 ConfigBasedSwaggerEndpointProvider 替代 YarpStateSwaggerEndpointProvider
        // 这样可以直接从配置文件读取集群信息，不依赖 YARP 运行时状态
        services.TryAddSingleton<ISwaggerEndpointProvider, ConfigBasedSwaggerEndpointProvider>();
        services.TryAddSingleton<ISwaggerDocumentLoader, HttpSwaggerDocumentLoader>();
        services.TryAddSingleton<ISwaggerDocumentMerger, DefaultSwaggerDocumentMerger>();
        services.TryAddSingleton<ISwaggerAggregator, SwaggerAggregator>();
        services.TryAddSingleton<IAggregatedDocumentStore, InMemoryAggregatedDocumentStore>();

        // 默认文档转换器（路径前缀和过滤）
        services.AddSingleton<ISwaggerDocumentTransformer, PathPrefixTransformer>();
        services.AddSingleton<ISwaggerDocumentTransformer, PathFilterTransformer>();

        // 后台刷新服务
        services.AddHostedService<SwaggerRefreshService>();

        // 注册 AggregatedSwaggerProvider 作为 Swashbuckle 的 ISwaggerProvider
        // 这将完全绕过 SwaggerDoc 预注册机制，实现动态文档发现
        services.TryAddSingleton<AggregatedSwaggerProvider>();
        services.AddSingleton<IAsyncSwaggerProvider>(sp => sp.GetRequiredService<AggregatedSwaggerProvider>());
        services.AddSingleton<ISwaggerProvider>(sp => sp.GetRequiredService<AggregatedSwaggerProvider>());

        // 遥测
        services.AddSwaggerTelemetry();

        // 应用自定义配置
        if (configure != null)
        {
            var aggregationBuilder = new SwaggerAggregationBuilder(services);
            configure(aggregationBuilder);
            aggregationBuilder.Build();
        }

        return builder;
    }

    /// <summary>
    /// 添加 Swagger 聚合功能（带配置）
    /// </summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationOptions> configureOptions,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        builder.Services.Configure(configureOptions);
        return builder.AddSwaggerAggregation(configure);
    }
}
