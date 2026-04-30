namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Discovers Swagger endpoints from registered <see cref="ISwaggerEndpointSource"/>s and
/// resolves their addresses through registered <see cref="ISwaggerEndpointAddressResolver"/>s.
/// </summary>
public interface ISwaggerEndpointDiscoveryService
{
    /// <summary>
    /// Run a full discovery pass.
    /// </summary>
    /// <param name="documentName">
    /// When non-null, only endpoints whose effective document name matches are returned.
    /// Diagnostics are not filtered.
    /// </param>
    ValueTask<SwaggerEndpointDiscoveryResult> DiscoverAsync(
        string? documentName = null,
        CancellationToken cancellationToken = default);
}
