using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Stores aggregated OpenAPI documents.
/// </summary>
public interface IAggregatedDocumentStore
{
    /// <summary>
    /// Gets a cached aggregated document asynchronously when available.
    /// </summary>
    ValueTask<OpenApiDocument?> GetAsync(string documentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an aggregated document.
    /// </summary>
    ValueTask SetAsync(string documentName, OpenApiDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all cached document names.
    /// </summary>
    IReadOnlyList<string> GetDocumentNames();
}
