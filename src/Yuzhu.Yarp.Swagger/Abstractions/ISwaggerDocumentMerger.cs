using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Merges per-endpoint load results into a single OpenAPI document.
/// </summary>
public interface ISwaggerDocumentMerger
{
    /// <summary>
    /// Merge <paramref name="sources"/> into a single OpenAPI document.
    /// </summary>
    OpenApiDocument Merge(
        string documentName,
        IReadOnlyList<SwaggerLoadResult> sources,
        SwaggerMergeOptions options);
}

/// <summary>
/// Per-merge tunables.
/// </summary>
public sealed record SwaggerMergeOptions
{
    /// <summary>
    /// When <c>true</c> a warning footer listing failed services is appended to the merged
    /// document description. Off by default - aggregator output should describe the merged
    /// API, not the merge process.
    /// </summary>
    public bool IncludeFailedServicesWarning { get; init; }
}
