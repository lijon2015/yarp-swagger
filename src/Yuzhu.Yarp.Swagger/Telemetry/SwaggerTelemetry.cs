using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Yuzhu.Yarp.Swagger.Telemetry;

/// <summary>
/// Activity source, meter, and signals exposed by the Swagger aggregator. Tag names follow
/// the naming convention from the long-term plan: <c>cluster.id</c>, <c>document.name</c>,
/// <c>destination.address</c>, <c>swagger.path</c>, <c>http.status_code</c>,
/// <c>failure.stage</c>, <c>failure.reason</c>, <c>from.cache</c>.
/// </summary>
public static class SwaggerTelemetry
{
    /// <summary>Activity / meter source name.</summary>
    public const string ServiceName = "Yuzhu.Yarp.Swagger";

    /// <summary>Telemetry version. Bumped with the package major version.</summary>
    public const string Version = "3.0.0";

    /// <summary>Activity source for distributed tracing.</summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, Version);

    private static readonly Meter Meter = new(ServiceName, Version);

    /// <summary>Counter incremented when a refresh pass completes.</summary>
    public static readonly Counter<long> RefreshCounter =
        Meter.CreateCounter<long>(
            "swagger.refresh.count",
            description: "Number of swagger refresh operations");

    /// <summary>Counter incremented when a cluster is skipped during discovery.</summary>
    public static readonly Counter<long> DiscoverySkippedCounter =
        Meter.CreateCounter<long>(
            "swagger.discovery.skipped",
            description: "Number of clusters skipped during discovery");

    /// <summary>Counter incremented when a single document load succeeds.</summary>
    public static readonly Counter<long> LoadSuccessCounter =
        Meter.CreateCounter<long>(
            "swagger.load.success",
            description: "Number of successful swagger document loads");

    /// <summary>Counter incremented when a single document load fails.</summary>
    public static readonly Counter<long> LoadFailureCounter =
        Meter.CreateCounter<long>(
            "swagger.load.failure",
            description: "Number of failed swagger document loads");

    /// <summary>Counter incremented when a cached document is served.</summary>
    public static readonly Counter<long> CacheHitCounter =
        Meter.CreateCounter<long>(
            "swagger.cache.hit",
            description: "Number of cache hits when serving swagger documents");

    /// <summary>Histogram of single-document load duration.</summary>
    public static readonly Histogram<double> LoadDuration =
        Meter.CreateHistogram<double>(
            "swagger.load.duration",
            unit: "ms",
            description: "Duration of swagger document load operations");

    /// <summary>Histogram of full refresh-pass duration.</summary>
    public static readonly Histogram<double> RefreshDuration =
        Meter.CreateHistogram<double>(
            "swagger.refresh.duration",
            unit: "ms",
            description: "Duration of swagger refresh operations");

    private static int _endpointCount;

    /// <summary>Observable gauge reporting the discovered endpoint count.</summary>
    public static readonly ObservableGauge<int> EndpointCount =
        Meter.CreateObservableGauge(
            "swagger.endpoints.count",
            () => _endpointCount,
            description: "Number of swagger endpoints discovered in the last refresh");

    /// <summary>Update the endpoint-count gauge.</summary>
    public static void SetEndpointCount(int count) => _endpointCount = count;
}
