using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// 存储聚合后的 OpenAPI 文档
/// </summary>
public interface IAggregatedDocumentStore
{
    /// <summary>
    /// 同步获取聚合文档
    /// </summary>
    OpenApiDocument? Get(string documentName);

    /// <summary>
    /// 异步获取聚合文档
    /// </summary>
    ValueTask<OpenApiDocument?> GetAsync(string documentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 存储聚合文档
    /// </summary>
    ValueTask SetAsync(string documentName, OpenApiDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查文档是否存在
    /// </summary>
    bool Exists(string documentName);

    /// <summary>
    /// 获取所有文档名称
    /// </summary>
    IReadOnlyList<string> GetDocumentNames();
}
