using Microsoft.Extensions.Logging;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Discovery;

internal static class SwaggerEndpointDiscoveryHelper
{
    public static bool IsSwaggerEnabled(Func<string, string?> getMetadataValue) => IsTrue(getMetadataValue(MetadataKeys.Enabled));

    public static bool MatchesDocumentName(SwaggerEndpoint endpoint, string documentName)
    {
        return string.Equals(
            GetEffectiveDocumentName(endpoint),
            documentName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static string GetEffectiveDocumentName(SwaggerEndpoint endpoint) => endpoint.DocumentName ?? endpoint.ClusterId;

    public static string GetEndpointIdentity(SwaggerEndpoint endpoint)
    {
        return string.Create(
            endpoint.ClusterId.Length + GetEffectiveDocumentName(endpoint).Length + 1,
            endpoint,
            static (span, value) =>
            {
                value.ClusterId.AsSpan().CopyTo(span);
                span[value.ClusterId.Length] = '|';
                GetEffectiveDocumentName(value).AsSpan().CopyTo(span[(value.ClusterId.Length + 1)..]);
            });
    }

    public static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static SwaggerEndpoint? CreateEndpoint(
        string clusterId,
        string? baseAddress,
        Func<string, string?> getMetadataValue,
        string defaultSwaggerPath,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            logger.LogWarning("Cluster {ClusterId} has no valid destination address", clusterId);
            return null;
        }

        try
        {
            SwaggerEndpoint endpoint = new SwaggerEndpoint
            {
                ClusterId = clusterId,
                BaseAddress = new Uri(baseAddress),
                SwaggerPath = getMetadataValue(MetadataKeys.Path) ?? defaultSwaggerPath,
                PathPrefix = getMetadataValue(MetadataKeys.Prefix),
                PathFilter = getMetadataValue(MetadataKeys.PathFilter),
                AccessTokenClient = getMetadataValue(MetadataKeys.AccessTokenClient),
                IsMetadataSource = IsTrue(getMetadataValue(MetadataKeys.IsMetadataSource)),
                DocumentName = getMetadataValue(MetadataKeys.DocumentName)
            };

            logger.LogDebug(
                "Discovered Swagger endpoint for cluster {ClusterId}: {SwaggerUrl}",
                clusterId,
                endpoint.SwaggerUrl);

            return endpoint;
        }
        catch (UriFormatException ex)
        {
            logger.LogWarning(
                ex,
                "Invalid base address for cluster {ClusterId}: {Address}",
                clusterId,
                baseAddress);

            return null;
        }
    }
}
