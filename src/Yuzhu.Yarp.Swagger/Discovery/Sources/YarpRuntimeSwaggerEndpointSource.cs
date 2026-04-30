using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery.Metadata;

namespace Yuzhu.Yarp.Swagger.Discovery.Sources;

/// <summary>
/// Reads cluster candidates from live YARP proxy state via
/// <see cref="IProxyStateLookup"/>. Preferred over the configuration source because it
/// reflects the actual runtime cluster shape (including dynamic destinations supplied by
/// custom <c>IProxyConfigProvider</c>s).
/// </summary>
public sealed class YarpRuntimeSwaggerEndpointSource(
    IProxyStateLookup proxyState) : ISwaggerEndpointSource
{
    private readonly IProxyStateLookup _proxyState = proxyState;

    public ValueTask<IReadOnlyList<SwaggerClusterCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        List<SwaggerClusterCandidate> candidates = [];

        foreach (ClusterState cluster in _proxyState.GetClusters())
        {
            DictionarySwaggerMetadataAccessor metadata = new(cluster.Model.Config.Metadata);

            candidates.Add(new SwaggerClusterCandidate(
                ClusterId: cluster.ClusterId,
                DocumentName: metadata.Get(MetadataKeys.DocumentName),
                Metadata: metadata,
                NativeCluster: cluster));
        }

        return ValueTask.FromResult<IReadOnlyList<SwaggerClusterCandidate>>(candidates);
    }
}
