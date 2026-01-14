using System.ComponentModel.DataAnnotations;

namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Swagger 聚合配置选项
/// </summary>
public sealed record SwaggerAggregationOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "SwaggerAggregation";

    /// <summary>
    /// 后台刷新间隔
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "24:00:00")]
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 单个文档加载超时时间
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
    public TimeSpan LoadTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 聚合超时时间（整体聚合过程的最大时间）
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:30", "00:10:00")]
    public TimeSpan AggregationTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 最大并行加载数
    /// </summary>
    [Range(1, 50)]
    public int MaxParallelism { get; init; } = 10;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// 默认 Swagger 路径（当 Metadata 未指定时）
    /// </summary>
    [MaxLength(200)]
    public string DefaultSwaggerPath { get; init; } = "/swagger/v1/swagger.json";

    /// <summary>
    /// 启动时预热等待时间
    /// </summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 最大文档大小（字节）- 防止加载过大文档导致内存溢出
    /// </summary>
    [Range(1024, 100 * 1024 * 1024)] // 1KB to 100MB
    public int MaxDocumentSizeBytes { get; init; } = SwaggerConstants.MaxDocumentSizeBytes;
}
