using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Yuzhu.Yarp.Swagger.Telemetry;

/// <summary>
/// Swagger 聚合遥测
/// </summary>
public static class SwaggerTelemetry
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public const string ServiceName = "Yarp.Swagger.Aggregation";

    /// <summary>
    /// 版本
    /// </summary>
    public const string Version = "2.0.0";

    /// <summary>
    /// Activity 源
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, Version);

    private static readonly Meter Meter = new(ServiceName, Version);

    /// <summary>
    /// 刷新计数器
    /// </summary>
    public static readonly Counter<long> RefreshCounter =
        Meter.CreateCounter<long>(
            "swagger.refresh.count",
            description: "Number of swagger refresh operations");

    /// <summary>
    /// 加载成功计数器
    /// </summary>
    public static readonly Counter<long> LoadSuccessCounter =
        Meter.CreateCounter<long>(
            "swagger.load.success",
            description: "Number of successful swagger document loads");

    /// <summary>
    /// 加载失败计数器
    /// </summary>
    public static readonly Counter<long> LoadFailureCounter =
        Meter.CreateCounter<long>(
            "swagger.load.failure",
            description: "Number of failed swagger document loads");

    /// <summary>
    /// 缓存命中计数器
    /// </summary>
    public static readonly Counter<long> CacheHitCounter =
        Meter.CreateCounter<long>(
            "swagger.cache.hit",
            description: "Number of cache hits when serving swagger documents");

    /// <summary>
    /// 加载耗时直方图
    /// </summary>
    public static readonly Histogram<double> LoadDuration =
        Meter.CreateHistogram<double>(
            "swagger.load.duration",
            unit: "ms",
            description: "Duration of swagger document load operations");

    /// <summary>
    /// 刷新耗时直方图
    /// </summary>
    public static readonly Histogram<double> RefreshDuration =
        Meter.CreateHistogram<double>(
            "swagger.refresh.duration",
            unit: "ms",
            description: "Duration of swagger refresh operations");

    private static int _endpointCount;

    /// <summary>
    /// 端点数量仪表
    /// </summary>
    public static readonly ObservableGauge<int> EndpointCount =
        Meter.CreateObservableGauge(
            "swagger.endpoints.count",
            () => _endpointCount,
            description: "Number of swagger endpoints discovered");

    /// <summary>
    /// 设置端点数量
    /// </summary>
    public static void SetEndpointCount(int count) => _endpointCount = count;
}
