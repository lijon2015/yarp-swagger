namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// Thrown by <see cref="SwaggerAggregator"/> when at least one endpoint was discovered for
/// a document but every backend load failed. The coordinator translates this into a
/// "known but unavailable" resolution; the aggregation middleware turns that into the
/// configured 5xx status (default 503).
/// </summary>
public sealed class SwaggerAggregationFailedException(
    string documentName,
    int endpointCount,
    string reason)
    : InvalidOperationException(
        $"Swagger aggregation for '{documentName}' failed: every backend load failed " +
        $"({endpointCount} endpoint(s)). Reason: {reason}")
{
    /// <summary>Logical document name being aggregated.</summary>
    public string DocumentName { get; } = documentName;

    /// <summary>Number of endpoints in the failing aggregation group.</summary>
    public int EndpointCount { get; } = endpointCount;

    /// <summary>Short comma-separated cluster|stage|status summary.</summary>
    public string Reason { get; } = reason;
}
