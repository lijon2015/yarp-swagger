using System.ComponentModel.DataAnnotations;

namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Aggregation pipeline tunables. Bound from the <see cref="SectionName"/> section by default.
/// </summary>
public sealed class SwaggerAggregationOptions
{
    /// <summary>Default configuration section.</summary>
    public const string SectionName = "SwaggerAggregation";

    /// <summary>Periodic refresh interval used by the periodic refresh trigger.</summary>
    [Range(typeof(TimeSpan), "00:00:10", "24:00:00")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Per-attempt timeout used by the resilience pipeline.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Outer aggregation timeout (covers all endpoints for one document).</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:30:00")]
    public TimeSpan AggregationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Maximum parallel document loads.</summary>
    [Range(1, 50)]
    public int MaxParallelism { get; set; } = 10;

    /// <summary>Maximum retry attempts in the resilience pipeline.</summary>
    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Default Swagger path used when metadata <c>Swagger:Path</c> is absent.</summary>
    [MaxLength(200)]
    public string DefaultSwaggerPath { get; set; } = SwaggerConstants.DefaultSwaggerPath;

    /// <summary>Initial delay before first periodic refresh.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Maximum allowed document size, in bytes.</summary>
    [Range(1024, 100 * 1024 * 1024)]
    public int MaxDocumentSizeBytes { get; set; } = SwaggerConstants.DefaultMaxDocumentSizeBytes;

    /// <summary>
    /// Append a footer to the merged document description listing services that failed to
    /// load. Off by default - aggregator output should describe the merged API, not the
    /// merge process. Diagnostics should be consumed via logs/metrics.
    /// </summary>
    public bool IncludeFailedServicesWarning { get; set; }
}
