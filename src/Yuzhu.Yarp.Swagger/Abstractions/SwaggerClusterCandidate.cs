namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// A cluster discovered by an <see cref="ISwaggerEndpointSource"/>. Address resolution and
/// metadata reads happen against this record; <see cref="NativeCluster"/> lets resolvers
/// pattern-match on the underlying object (e.g. <c>ClusterState</c> or
/// <c>IConfigurationSection</c>).
/// </summary>
/// <param name="ClusterId">YARP cluster identifier.</param>
/// <param name="DocumentName">
/// Optional document name override read from metadata. When <c>null</c>, the discovery
/// service falls back to <see cref="ClusterId"/>.
/// </param>
/// <param name="Metadata">Metadata accessor for the candidate cluster.</param>
/// <param name="NativeCluster">Source-specific cluster representation, if any.</param>
public sealed record SwaggerClusterCandidate(
    string ClusterId,
    string? DocumentName,
    ISwaggerMetadataAccessor Metadata,
    object? NativeCluster = null);
