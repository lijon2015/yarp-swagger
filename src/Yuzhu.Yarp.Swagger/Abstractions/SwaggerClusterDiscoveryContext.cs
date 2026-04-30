namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Per-cluster state passed to <see cref="ISwaggerEndpointAddressResolver"/>s.
/// </summary>
/// <param name="Candidate">The candidate being resolved.</param>
/// <param name="DefaultSwaggerPath">
/// Default Swagger path from <see cref="Configuration.SwaggerAggregationOptions.DefaultSwaggerPath"/>;
/// resolvers do not normally need this but it is exposed for completeness.
/// </param>
public sealed record SwaggerClusterDiscoveryContext(
    SwaggerClusterCandidate Candidate,
    string DefaultSwaggerPath);
