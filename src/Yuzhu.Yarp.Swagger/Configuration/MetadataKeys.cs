namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Canonical YARP cluster metadata keys read by the Swagger aggregator. The discovery
/// pipeline always reads using these flat keys; <see cref="Abstractions.ISwaggerMetadataAccessor"/>
/// implementations are responsible for translating them when the underlying store uses
/// nested configuration paths.
/// </summary>
public static class MetadataKeys
{
    /// <summary>Enables Swagger aggregation for a cluster (case-insensitive <c>true</c>/<c>false</c>).</summary>
    public const string Enabled = "Swagger:Enabled";

    /// <summary>Overrides the Swagger document path on the backend.</summary>
    public const string Path = "Swagger:Path";

    /// <summary>Adds a prefix to all transformed paths.</summary>
    public const string Prefix = "Swagger:Prefix";

    /// <summary>Filters paths with a regex pattern.</summary>
    public const string PathFilter = "Swagger:PathFilter";

    /// <summary>Marks a cluster as the metadata source for the merged document.</summary>
    public const string IsMetadataSource = "Swagger:IsMetadataSource";

    /// <summary>OAuth client name used to acquire an access token.</summary>
    public const string AccessTokenClient = "Swagger:AccessTokenClient";

    /// <summary>Logical document group name. When absent the cluster id is used.</summary>
    public const string DocumentName = "Swagger:DocumentName";
}
