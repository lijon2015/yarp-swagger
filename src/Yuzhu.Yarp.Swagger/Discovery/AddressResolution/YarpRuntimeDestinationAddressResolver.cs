using System.Diagnostics.CodeAnalysis;
using Yarp.ReverseProxy.Model;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Discovery.AddressResolution;

/// <summary>
/// Resolves the base address from a YARP runtime <see cref="ClusterState"/>. Prefers
/// available destinations (those reporting healthy in the YARP runtime); falls back to any
/// destination configured on the cluster when nothing is available.
/// </summary>
public sealed class YarpRuntimeDestinationAddressResolver : ISwaggerEndpointAddressResolver
{
    public ValueTask<SwaggerAddressResolution> ResolveAsync(
        SwaggerClusterDiscoveryContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Candidate.NativeCluster is not ClusterState cluster)
        {
            return ValueTask.FromResult(SwaggerAddressResolution.NotApplicable);
        }

        DestinationState? available = cluster.DestinationsState?.AvailableDestinations.FirstOrDefault();
        if (available is not null
            && TryParseAbsoluteHttpUri(available.Model.Config.Address, out Uri? availableUri))
        {
            return ValueTask.FromResult(SwaggerAddressResolution.Resolve(availableUri));
        }

        if (cluster.Model.Config.Destinations is { Count: > 0 } destinations)
        {
            string? configuredAddress = destinations.Values
                .Select(static d => d.Address)
                .FirstOrDefault(static a => !string.IsNullOrWhiteSpace(a));

            if (TryParseAbsoluteHttpUri(configuredAddress, out Uri? configuredUri))
            {
                return ValueTask.FromResult(SwaggerAddressResolution.Resolve(configuredUri));
            }
        }

        return ValueTask.FromResult(SwaggerAddressResolution.Skipped(
            "Cluster has no available or configured destinations in runtime state"));
    }

    private static bool TryParseAbsoluteHttpUri(string? address, [NotNullWhen(true)] out Uri? uri)
    {
        if (string.IsNullOrWhiteSpace(address)
            || !Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
            || parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            uri = null;
            return false;
        }

        uri = parsed;
        return true;
    }
}
