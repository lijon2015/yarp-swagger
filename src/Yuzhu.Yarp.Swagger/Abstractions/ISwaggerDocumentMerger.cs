using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// 负责将多个 Swagger 文档合并为一个
/// </summary>
public interface ISwaggerDocumentMerger
{
    /// <summary>
    /// 合并多个 Swagger 文档
    /// </summary>
    OpenApiDocument Merge(
        IEnumerable<SwaggerLoadResult> sources,
        MergeOptions options);
}
