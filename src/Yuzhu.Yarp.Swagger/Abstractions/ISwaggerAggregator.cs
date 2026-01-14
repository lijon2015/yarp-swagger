using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Swagger 文档聚合服务的主入口
/// </summary>
public interface ISwaggerAggregator
{
    /// <summary>
    /// 聚合多个端点的 Swagger 文档
    /// </summary>
    Task<OpenApiDocument> AggregateAsync(
        AggregationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 聚合上下文
/// </summary>
public sealed record AggregationContext
{
    /// <summary>
    /// 待聚合的端点列表
    /// </summary>
    public required IReadOnlyList<SwaggerEndpoint> Endpoints { get; init; }

    /// <summary>
    /// 合并选项
    /// </summary>
    public MergeOptions MergeOptions { get; init; } = new();

    /// <summary>
    /// 文档名称
    /// </summary>
    public string? DocumentName { get; init; }
}

/// <summary>
/// 文档合并选项
/// </summary>
public sealed record MergeOptions
{
    /// <summary>
    /// 是否在文档中包含失败服务的警告
    /// </summary>
    public bool IncludeFailedServicesWarning { get; init; } = true;
}
