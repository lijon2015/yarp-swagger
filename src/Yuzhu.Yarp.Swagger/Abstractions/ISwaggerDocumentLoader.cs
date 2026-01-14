using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// 负责从远程服务加载 Swagger 文档
/// </summary>
public interface ISwaggerDocumentLoader
{
    /// <summary>
    /// 异步加载 Swagger 文档
    /// </summary>
    Task<SwaggerLoadResult> LoadAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Swagger 文档加载结果
/// </summary>
public sealed record SwaggerLoadResult
{
    /// <summary>
    /// 端点信息
    /// </summary>
    public required SwaggerEndpoint Endpoint { get; init; }

    /// <summary>
    /// 加载的文档（成功时非空）
    /// </summary>
    public OpenApiDocument? Document { get; init; }

    /// <summary>
    /// 是否加载成功
    /// </summary>
    public bool IsSuccess => Document is not null;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 加载耗时
    /// </summary>
    public TimeSpan LoadDuration { get; init; }
}
