using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// 文档转换管道中的单个转换器
/// </summary>
public interface ISwaggerDocumentTransformer
{
    /// <summary>
    /// 执行顺序（越小越先执行）
    /// </summary>
    int Order => 0;

    /// <summary>
    /// 转换文档
    /// </summary>
    ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 转换上下文
/// </summary>
public sealed record TransformContext
{
    /// <summary>
    /// Cluster ID
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// 端点信息
    /// </summary>
    public required SwaggerEndpoint Endpoint { get; init; }

    /// <summary>
    /// 转换参数
    /// </summary>
    public IReadOnlyDictionary<string, string>? TransformValues { get; init; }
}
