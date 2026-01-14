using Microsoft.Extensions.DependencyInjection;

namespace Yuzhu.Yarp.Swagger.Telemetry;

/// <summary>
/// OpenTelemetry 扩展方法
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// 添加 Swagger 聚合遥测
    /// </summary>
    public static IServiceCollection AddSwaggerTelemetry(this IServiceCollection services)
    {
        // 预注册遥测组件（确保计数器在使用前被创建）
        _ = SwaggerTelemetry.ActivitySource;
        _ = SwaggerTelemetry.RefreshCounter;
        _ = SwaggerTelemetry.LoadSuccessCounter;
        _ = SwaggerTelemetry.LoadFailureCounter;
        _ = SwaggerTelemetry.CacheHitCounter;
        _ = SwaggerTelemetry.LoadDuration;
        _ = SwaggerTelemetry.RefreshDuration;
        _ = SwaggerTelemetry.EndpointCount;

        return services;
    }
}
