using Microsoft.Extensions.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery.Metadata;

namespace Yuzhu.Yarp.Swagger.Discovery.Sources;

/// <summary>
/// Reads cluster candidates from <see cref="IConfiguration"/> at <c>ReverseProxy:Clusters</c>
/// (or <c>Yarp:Clusters</c>). Used as a fallback when YARP runtime state has no destinations
/// yet, or when the project does not feed runtime data through YARP at all.
/// </summary>
public sealed class YarpConfigurationSwaggerEndpointSource(
    IConfiguration configuration) : ISwaggerEndpointSource
{
    private readonly IConfiguration _configuration = configuration;

    public ValueTask<IReadOnlyList<SwaggerClusterCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        IConfigurationSection? clustersSection = ResolveClustersSection();
        if (clustersSection is null)
        {
            return ValueTask.FromResult<IReadOnlyList<SwaggerClusterCandidate>>([]);
        }

        List<SwaggerClusterCandidate> candidates = [];

        foreach (IConfigurationSection clusterSection in clustersSection.GetChildren())
        {
            IConfigurationSection metadataSection = clusterSection.GetSection("Metadata");
            ConfigurationSwaggerMetadataAccessor metadata = new(metadataSection);

            candidates.Add(new SwaggerClusterCandidate(
                ClusterId: clusterSection.Key,
                DocumentName: metadata.Get(MetadataKeys.DocumentName),
                Metadata: metadata,
                NativeCluster: clusterSection));
        }

        return ValueTask.FromResult<IReadOnlyList<SwaggerClusterCandidate>>(candidates);
    }

    private IConfigurationSection? ResolveClustersSection()
    {
        foreach (string path in SwaggerConstants.YarpClusterConfigSections)
        {
            IConfigurationSection section = _configuration.GetSection(path);
            if (section.Exists())
            {
                return section;
            }
        }

        return null;
    }
}
