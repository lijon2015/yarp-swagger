namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// A discovered Swagger endpoint ready for loading and aggregation.
/// </summary>
public sealed record SwaggerEndpoint
{
    /// <summary>YARP cluster identifier that produced this endpoint.</summary>
    public required string ClusterId { get; init; }

    /// <summary>Logical aggregated document name.</summary>
    public required string DocumentName { get; init; }

    /// <summary>Resolved base address used to load the Swagger document.</summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>Relative path to the Swagger JSON document on the backend.</summary>
    public required string SwaggerPath { get; init; }

    /// <summary>Optional path prefix applied during transformation.</summary>
    public string? PathPrefix { get; init; }

    /// <summary>Optional regex used to filter paths during transformation.</summary>
    public string? PathFilter { get; init; }

    /// <summary>Optional OAuth client name used to acquire an access token.</summary>
    public string? AccessTokenClient { get; init; }

    /// <summary>Whether this endpoint provides info/metadata for the merged document.</summary>
    public bool IsMetadataSource { get; init; }

    /// <summary>Absolute URL pointing at the Swagger document.</summary>
    public Uri SwaggerUrl => new(BaseAddress, SwaggerPath);
}
