namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Output of a discovery pass.
/// </summary>
/// <param name="Endpoints">All endpoints that resolved successfully.</param>
/// <param name="Diagnostics">Per-cluster diagnostic records explaining keeps and skips.</param>
public sealed record SwaggerEndpointDiscoveryResult(
    IReadOnlyList<SwaggerEndpoint> Endpoints,
    IReadOnlyList<SwaggerEndpointDiagnostic> Diagnostics)
{
    /// <summary>An empty result with no endpoints and no diagnostics.</summary>
    public static SwaggerEndpointDiscoveryResult Empty { get; } =
        new([], []);
}
