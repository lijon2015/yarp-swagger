using Microsoft.Extensions.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Discovery.AddressResolution;

/// <summary>
/// Resolves the base address from a configuration <see cref="IConfigurationSection"/> that
/// represents a YARP cluster. Reads the first <c>Destinations:*:Address</c> value and
/// then falls back to the gateway's <c>FallbackDestinations:*:Address</c> convention.
/// </summary>
public sealed class YarpConfiguredDestinationAddressResolver : ISwaggerEndpointAddressResolver
{
    public ValueTask<SwaggerAddressResolution> ResolveAsync(
        SwaggerClusterDiscoveryContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Candidate.NativeCluster is not IConfigurationSection clusterSection)
        {
            return ValueTask.FromResult(SwaggerAddressResolution.NotApplicable);
        }

        SwaggerAddressResolution destinations = ResolveSection(
            clusterSection,
            "Destinations",
            "Destination");

        if (destinations.Resolved)
        {
            return ValueTask.FromResult(destinations);
        }

        SwaggerAddressResolution fallbackDestinations = ResolveSection(
            clusterSection,
            "FallbackDestinations",
            "Fallback destination");

        if (fallbackDestinations.Resolved)
        {
            return ValueTask.FromResult(fallbackDestinations);
        }

        bool hasDestinations = clusterSection.GetSection("Destinations").Exists();
        bool hasFallbackDestinations = clusterSection.GetSection("FallbackDestinations").Exists();
        SwaggerAddressResolution result = !hasDestinations && !hasFallbackDestinations
            ? SwaggerAddressResolution.Skipped(
                "Cluster configuration has no Destinations or FallbackDestinations section")
            : hasFallbackDestinations
            ? fallbackDestinations
            : destinations;

        return ValueTask.FromResult(result);
    }

    private static SwaggerAddressResolution ResolveSection(
        IConfigurationSection clusterSection,
        string sectionName,
        string diagnosticName)
    {
        IConfigurationSection destinations = clusterSection.GetSection(sectionName);
        if (!destinations.Exists())
        {
            return SwaggerAddressResolution.Skipped(
                $"Cluster configuration has no {sectionName} section");
        }

        string? address = destinations.GetChildren()
            .Select(static d => d["Address"])
            .FirstOrDefault(static a => !string.IsNullOrWhiteSpace(a));

        return string.IsNullOrWhiteSpace(address)
            ? SwaggerAddressResolution.Skipped(
                $"No {sectionName} entry has a non-empty Address")
            : !Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
            || parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps
            ? SwaggerAddressResolution.Skipped(
                $"{diagnosticName} address '{address}' is not a valid http(s) URI")
            : SwaggerAddressResolution.Resolve(parsed);
    }
}
