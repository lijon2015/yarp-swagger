namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// YARP cluster metadata keys used by Swagger aggregation.
/// </summary>
public static class MetadataKeys
{
    /// <summary>
    /// Enables Swagger aggregation for a cluster.
    /// </summary>
    public const string Enabled = "Swagger:Enabled";

    /// <summary>
    /// Overrides the Swagger document path.
    /// </summary>
    public const string Path = "Swagger:Path";

    /// <summary>
    /// Adds a prefix to all transformed paths.
    /// </summary>
    public const string Prefix = "Swagger:Prefix";

    /// <summary>
    /// Filters paths with a regex pattern.
    /// </summary>
    public const string PathFilter = "Swagger:PathFilter";

    /// <summary>
    /// Marks a cluster as the metadata source for a merged document.
    /// </summary>
    public const string IsMetadataSource = "Swagger:IsMetadataSource";

    /// <summary>
    /// Specifies the OAuth client used to acquire an access token.
    /// </summary>
    public const string AccessTokenClient = "Swagger:AccessTokenClient";

    /// <summary>
    /// Overrides the logical document group name.
    /// </summary>
    public const string DocumentName = "Swagger:DocumentName";
}
