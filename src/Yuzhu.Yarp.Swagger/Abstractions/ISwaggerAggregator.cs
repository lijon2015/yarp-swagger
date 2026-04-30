using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Loads, transforms, and merges all endpoints in <see cref="AggregationContext"/> into one
/// OpenAPI document.
/// </summary>
public interface ISwaggerAggregator
{
    /// <summary>Aggregate all endpoints in the context.</summary>
    Task<OpenApiDocument> AggregateAsync(
        AggregationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inputs to a single aggregation pass.
/// </summary>
public sealed record AggregationContext
{
    /// <summary>Logical document name being aggregated.</summary>
    public required string DocumentName { get; init; }

    /// <summary>Endpoints contributing to this document.</summary>
    public required IReadOnlyList<SwaggerEndpoint> Endpoints { get; init; }

    /// <summary>Merge options applied at the final step.</summary>
    public SwaggerMergeOptions MergeOptions { get; init; } = new();
}
