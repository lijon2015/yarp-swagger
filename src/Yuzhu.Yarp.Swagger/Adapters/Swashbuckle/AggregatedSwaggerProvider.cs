using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// 聚合 Swagger 提供者 - 实现动态文档发现，绕过 SwaggerDoc 预注册机制
/// </summary>
public sealed class AggregatedSwaggerProvider :
    IAsyncSwaggerProvider,
    ISwaggerProvider
{
    private readonly IAggregatedDocumentStore _documentStore;
    private readonly ISwaggerEndpointProvider _endpointProvider;
    private readonly ISwaggerAggregator _aggregator;
    private readonly ILogger<AggregatedSwaggerProvider> _logger;

    public AggregatedSwaggerProvider(
        IAggregatedDocumentStore documentStore,
        ISwaggerEndpointProvider endpointProvider,
        ISwaggerAggregator aggregator,
        ILogger<AggregatedSwaggerProvider> logger)
    {
        _documentStore = documentStore;
        _endpointProvider = endpointProvider;
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// 异步获取文档 - Swagger 中间件优先使用此方法
    /// </summary>
    public async Task<OpenApiDocument> GetSwaggerAsync(
        string documentName,
        string? host = null,
        string? basePath = null)
    {
        // 首先尝试从缓存获取
        var cachedDoc = await _documentStore.GetAsync(documentName);
        if (cachedDoc != null)
        {
            SwaggerTelemetry.CacheHitCounter.Add(1,
                new KeyValuePair<string, object?>("document.name", documentName));
            _logger.LogDebug("Returning cached document for '{DocumentName}'", documentName);
            return cachedDoc;
        }

        // 缓存未命中 - 尝试按需加载（降级策略）
        _logger.LogInformation(
            "Document '{DocumentName}' not in cache, attempting on-demand load",
            documentName);

        var document = await LoadDocumentOnDemandAsync(documentName);
        if (document != null)
        {
            return document;
        }

        // 文档不存在 - 返回占位文档（用于启动期间或未知文档）
        _logger.LogWarning(
            "Document '{DocumentName}' not found, returning placeholder document",
            documentName);
        return CreatePlaceholderDocument(documentName);
    }

    /// <summary>
    /// 同步获取文档 - 作为后备
    /// </summary>
    public OpenApiDocument GetSwagger(
        string documentName,
        string? host = null,
        string? basePath = null)
    {
        var cachedDoc = _documentStore.Get(documentName);
        if (cachedDoc != null)
        {
            return cachedDoc;
        }

        // 返回占位文档（用于启动期间或未知文档）
        return CreatePlaceholderDocument(documentName);
    }

    /// <summary>
    /// 获取所有可用的文档名称
    /// </summary>
    public IReadOnlyList<string> GetDocumentNames()
    {
        // 优先从缓存获取
        var cachedNames = _documentStore.GetDocumentNames();
        if (cachedNames.Count > 0)
        {
            return cachedNames;
        }

        // 缓存为空时从 YARP 状态获取
        var endpoints = _endpointProvider.GetEndpoints();
        if (endpoints.Count > 0)
        {
            return endpoints
                .Select(e => e.DocumentName ?? e.ClusterId)
                .Distinct()
                .ToList();
        }

        // 返回空列表
        return [];
    }

    /// <summary>
    /// 按需加载文档（降级策略）
    /// </summary>
    private async Task<OpenApiDocument?> LoadDocumentOnDemandAsync(string documentName)
    {
        var endpoints = _endpointProvider.GetEndpoints(documentName);

        if (endpoints.Count == 0)
        {
            _logger.LogWarning("No endpoints found for document '{DocumentName}'", documentName);
            return null;
        }

        try
        {
            var context = new AggregationContext
            {
                DocumentName = documentName,
                Endpoints = endpoints
            };

            var document = await _aggregator.AggregateAsync(context);

            // 存入缓存
            await _documentStore.SetAsync(documentName, document);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load document '{DocumentName}' on demand", documentName);
            return null;
        }
    }

    /// <summary>
    /// 创建占位文档（用于启动期间或文档未就绪时）
    /// </summary>
    private static OpenApiDocument CreatePlaceholderDocument(string documentName)
    {
        return new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = documentName,
                Version = "1.0.0",
                Description = "**API documentation is being loaded. Please refresh in a few seconds.**\n\n" +
                              "The Swagger aggregation service is still initializing. " +
                              "This may happen during application startup or when backend services are not yet available."
            },
            Paths = new OpenApiPaths()
        };
    }
}
