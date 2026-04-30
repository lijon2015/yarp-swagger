namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Resolves a base address for a discovered cluster. Resolvers are run in registration order
/// and the first to return <see cref="SwaggerAddressResolution.Resolved"/> wins. Resolvers
/// that don't apply to a candidate should return <see cref="SwaggerAddressResolution.NotApplicable"/>.
/// </summary>
public interface ISwaggerEndpointAddressResolver
{
    /// <summary>
    /// Attempt to resolve the base address for the candidate.
    /// </summary>
    ValueTask<SwaggerAddressResolution> ResolveAsync(
        SwaggerClusterDiscoveryContext context,
        CancellationToken cancellationToken = default);
}
