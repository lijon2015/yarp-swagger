namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Discovers Swagger endpoints for aggregation.
/// </summary>
public interface ISwaggerEndpointProvider
{
    /// <summary>
    /// Gets all enabled Swagger endpoints.
    /// </summary>
    IReadOnlyList<SwaggerEndpoint> GetEndpoints();

    /// <summary>
    /// Gets endpoints for a specific document name.
    /// </summary>
    IReadOnlyList<SwaggerEndpoint> GetEndpoints(string documentName);
}

/// <summary>
/// Swagger endpoint metadata.
/// </summary>
public sealed record SwaggerEndpoint
{
    /// <summary>
    /// Cluster identifier.
    /// </summary>
    public required string ClusterId { get; init; }

    /// <summary>
    /// Service base address.
    /// </summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>
    /// Relative Swagger document path.
    /// </summary>
    public required string SwaggerPath { get; init; }

    /// <summary>
    /// Optional path prefix applied during transformation.
    /// </summary>
    public string? PathPrefix { get; init; }

    /// <summary>
    /// Optional path filter regex applied during transformation.
    /// </summary>
    public string? PathFilter { get; init; }

    /// <summary>
    /// Optional OAuth client name used to fetch an access token.
    /// </summary>
    public string? AccessTokenClient { get; init; }

    /// <summary>
    /// Whether this endpoint provides the merged document metadata.
    /// </summary>
    public bool IsMetadataSource { get; init; }

    /// <summary>
    /// Optional document group name.
    /// </summary>
    public string? DocumentName { get; init; }

    /// <summary>
    /// Absolute Swagger document URL.
    /// </summary>
    public Uri SwaggerUrl => new(BaseAddress, SwaggerPath);
}
