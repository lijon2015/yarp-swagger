using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Merging;

/// <summary>
/// 默认的 Swagger 文档合并器
/// </summary>
/// <remarks>
/// 此合并器只负责将多个已转换的文档合并为一个。
/// 路径前缀和过滤等转换操作由 ISwaggerDocumentTransformer 管道处理。
/// </remarks>
public sealed class DefaultSwaggerDocumentMerger : ISwaggerDocumentMerger
{
    private readonly ILogger<DefaultSwaggerDocumentMerger> _logger;

    public DefaultSwaggerDocumentMerger(ILogger<DefaultSwaggerDocumentMerger> logger)
    {
        _logger = logger;
    }

    public OpenApiDocument Merge(
        IEnumerable<SwaggerLoadResult> sources,
        MergeOptions options)
    {
        var resultDocument = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Aggregated API",
                Version = "1.0.0"
            },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents()
        };

        var failedServices = new List<string>();
        var securityRequirements = new List<OpenApiSecurityRequirement>();
        var tags = new HashSet<OpenApiTag>();

        foreach (var source in sources)
        {
            if (!source.IsSuccess)
            {
                failedServices.Add($"{source.Endpoint.ClusterId} ({source.ErrorMessage})");
                continue;
            }

            var document = source.Document!;
            var endpoint = source.Endpoint;

            // 如果是元数据源，使用其 Info
            if (endpoint.IsMetadataSource)
            {
                resultDocument.Info = document.Info;
            }

            // 合并组件
            MergeComponents(resultDocument.Components, document.Components, endpoint, _logger);

            // 合并路径
            MergePaths(resultDocument.Paths, document.Paths, endpoint, _logger);

            // 合并安全需求
            if (document.Security != null)
            {
                securityRequirements.AddRange(document.Security);
            }

            // 合并标签
            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    tags.Add(tag);
                }
            }
        }

        resultDocument.Security = securityRequirements;
        resultDocument.Tags = tags;

        // 添加失败服务警告
        if (failedServices.Count > 0 && options.IncludeFailedServicesWarning)
        {
            resultDocument.Info.Description =
                (resultDocument.Info.Description ?? "") +
                $"\n\n**Warning**: Failed to load Swagger for: {string.Join(", ", failedServices)}";

            _logger.LogWarning(
                "Aggregation completed with failures: {Services}",
                string.Join(", ", failedServices));
        }

        return resultDocument;
    }

    /// <summary>
    /// 合并路径（路径前缀和过滤已由 Transformer 管道处理）
    /// </summary>
    private static void MergePaths(
        OpenApiPaths targetPaths,
        OpenApiPaths? sourcePaths,
        SwaggerEndpoint endpoint,
        ILogger logger)
    {
        if (sourcePaths == null)
        {
            return;
        }

        foreach (var path in sourcePaths)
        {
            if (!targetPaths.TryAdd(path.Key, (OpenApiPathItem)path.Value))
            {
                logger.LogDebug(
                    "Path {Path} from {ClusterId} already exists, skipping",
                    path.Key, endpoint.ClusterId);
            }
        }
    }

    /// <summary>
    /// 合并组件（使用 FirstWins 策略）
    /// </summary>
    private static void MergeComponents(
        OpenApiComponents targetComponents,
        OpenApiComponents? sourceComponents,
        SwaggerEndpoint endpoint,
        ILogger logger)
    {
        if (sourceComponents == null)
        {
            return;
        }

        // Schemas
        MergeDictionary(
            targetComponents.Schemas ??= new Dictionary<string, IOpenApiSchema>(),
            sourceComponents.Schemas,
            endpoint.ClusterId,
            "Schema",
            logger);

        // SecuritySchemes
        MergeDictionary(
            targetComponents.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(),
            sourceComponents.SecuritySchemes,
            endpoint.ClusterId,
            "SecurityScheme",
            logger);

        // Parameters
        MergeDictionary(
            targetComponents.Parameters ??= new Dictionary<string, IOpenApiParameter>(),
            sourceComponents.Parameters,
            endpoint.ClusterId,
            "Parameter",
            logger);

        // Responses
        MergeDictionary(
            targetComponents.Responses ??= new Dictionary<string, IOpenApiResponse>(),
            sourceComponents.Responses,
            endpoint.ClusterId,
            "Response",
            logger);

        // RequestBodies
        MergeDictionary(
            targetComponents.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(),
            sourceComponents.RequestBodies,
            endpoint.ClusterId,
            "RequestBody",
            logger);

        // Headers
        MergeDictionary(
            targetComponents.Headers ??= new Dictionary<string, IOpenApiHeader>(),
            sourceComponents.Headers,
            endpoint.ClusterId,
            "Header",
            logger);

        // Examples
        MergeDictionary(
            targetComponents.Examples ??= new Dictionary<string, IOpenApiExample>(),
            sourceComponents.Examples,
            endpoint.ClusterId,
            "Example",
            logger);

        // Links
        MergeDictionary(
            targetComponents.Links ??= new Dictionary<string, IOpenApiLink>(),
            sourceComponents.Links,
            endpoint.ClusterId,
            "Link",
            logger);

        // Callbacks
        MergeDictionary(
            targetComponents.Callbacks ??= new Dictionary<string, IOpenApiCallback>(),
            sourceComponents.Callbacks,
            endpoint.ClusterId,
            "Callback",
            logger);

        // Extensions
        if (sourceComponents.Extensions != null)
        {
            targetComponents.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            foreach (var ext in sourceComponents.Extensions)
            {
                targetComponents.Extensions[ext.Key] = ext.Value;
            }
        }
    }

    /// <summary>
    /// 合并字典（使用 FirstWins 策略）
    /// </summary>
    private static void MergeDictionary<T>(
        IDictionary<string, T> target,
        IDictionary<string, T>? source,
        string clusterId,
        string componentType,
        ILogger logger)
    {
        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            if (!target.TryAdd(item.Key, item.Value))
            {
                logger.LogDebug(
                    "{ComponentType} conflict for {Key} from {ClusterId}. Ignored (First wins).",
                    componentType, item.Key, clusterId);
            }
        }
    }
}
