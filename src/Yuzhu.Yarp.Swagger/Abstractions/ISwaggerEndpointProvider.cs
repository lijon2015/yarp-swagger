namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// 从 YARP 运行时状态发现 Swagger 端点
/// </summary>
public interface ISwaggerEndpointProvider
{
    /// <summary>
    /// 获取所有启用了 Swagger 的端点
    /// </summary>
    IReadOnlyList<SwaggerEndpoint> GetEndpoints();

    /// <summary>
    /// 获取指定文档名称的端点
    /// </summary>
    IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName);
}

/// <summary>
/// Swagger 端点信息
/// </summary>
public sealed record SwaggerEndpoint
{
    /// <summary>
    /// Cluster ID
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// 服务基础地址
    /// </summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>
    /// Swagger 文档路径
    /// </summary>
    public required string SwaggerPath { get; init; }

    /// <summary>
    /// 路径前缀
    /// </summary>
    public string? PathPrefix { get; init; }

    /// <summary>
    /// 路径过滤正则表达式
    /// </summary>
    public string? PathFilter { get; init; }

    /// <summary>
    /// 访问令牌客户端名称
    /// </summary>
    public string? AccessTokenClient { get; init; }

    /// <summary>
    /// 是否只包含已发布的路径
    /// </summary>
    public bool OnlyPublishedPaths { get; init; }

    /// <summary>
    /// 是否作为元数据源（使用该服务的 Info）
    /// </summary>
    public bool IsMetadataSource { get; init; }

    /// <summary>
    /// 文档名称（用于分组）
    /// </summary>
    public string? DocumentName { get; init; }

    /// <summary>
    /// 完整的 Swagger URL
    /// </summary>
    public Uri SwaggerUrl => new(BaseAddress, SwaggerPath);

    /// <summary>
    /// HttpClient 名称
    /// </summary>
    internal string HttpClientName => $"swagger:{ClusterId}";
}
