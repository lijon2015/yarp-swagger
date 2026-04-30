namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Structured record describing why a cluster was kept, skipped, or failed during discovery.
/// </summary>
public sealed record SwaggerEndpointDiagnostic(
    string ClusterId,
    string Stage,
    string Severity,
    string Message,
    string? DocumentName = null,
    string? Address = null,
    string? SwaggerPath = null);

/// <summary>
/// Stages emitted by <see cref="ISwaggerEndpointDiscoveryService"/>.
/// </summary>
public static class SwaggerDiagnosticStage
{
    /// <summary>The endpoint source emitted (or failed to emit) candidates.</summary>
    public const string Source = "source";

    /// <summary>Metadata read on the candidate.</summary>
    public const string Metadata = "metadata";

    /// <summary>Address resolution against the candidate.</summary>
    public const string Address = "address";

    /// <summary>Validation of resolved endpoint values.</summary>
    public const string Validation = "validation";
}

/// <summary>
/// Severity levels for a diagnostic.
/// </summary>
public static class SwaggerDiagnosticSeverity
{
    /// <summary>Informational diagnostic.</summary>
    public const string Info = "info";

    /// <summary>The cluster was skipped but discovery is still healthy.</summary>
    public const string Warning = "warning";

    /// <summary>The cluster failed an expected step.</summary>
    public const string Error = "error";
}
