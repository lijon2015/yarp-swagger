namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Swagger 聚合相关的常量定义
/// </summary>
public static class SwaggerConstants
{
    /// <summary>
    /// 默认 HttpClient 名称
    /// </summary>
    public const string HttpClientName = "YarpSwagger";

    /// <summary>
    /// Swagger JSON 路径模板
    /// </summary>
    public const string SwaggerJsonPathTemplate = "/swagger/{0}/swagger.json";

    /// <summary>
    /// YARP 集群配置路径
    /// </summary>
    public const string YarpClustersConfigPath = "ReverseProxy:Clusters";

    /// <summary>
    /// 默认最大并行加载数
    /// </summary>
    public const int DefaultMaxParallelLoads = 10;

    /// <summary>
    /// 正则表达式超时时间（防止 ReDoS 攻击）
    /// </summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 最大路径过滤模式长度
    /// </summary>
    public const int MaxPathFilterLength = 500;

    /// <summary>
    /// 最大文档名称长度
    /// </summary>
    public const int MaxDocumentNameLength = 100;

    /// <summary>
    /// 最大文档大小（字节）- 默认 10MB
    /// </summary>
    public const int MaxDocumentSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// 默认聚合超时时间
    /// </summary>
    public static readonly TimeSpan DefaultAggregationTimeout = TimeSpan.FromMinutes(2);
}
