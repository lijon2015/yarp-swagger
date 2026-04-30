using Microsoft.Extensions.DependencyInjection;

namespace Yuzhu.Yarp.Swagger.Telemetry;

/// <summary>
/// OpenTelemetry registration helpers.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>Force initialization of telemetry signals so they appear before first use.</summary>
    public static IServiceCollection AddSwaggerTelemetry(this IServiceCollection services)
    {
        _ = SwaggerTelemetry.ActivitySource;
        _ = SwaggerTelemetry.RefreshCounter;
        _ = SwaggerTelemetry.DiscoverySkippedCounter;
        _ = SwaggerTelemetry.LoadSuccessCounter;
        _ = SwaggerTelemetry.LoadFailureCounter;
        _ = SwaggerTelemetry.CacheHitCounter;
        _ = SwaggerTelemetry.LoadDuration;
        _ = SwaggerTelemetry.RefreshDuration;
        _ = SwaggerTelemetry.EndpointCount;
        return services;
    }
}
