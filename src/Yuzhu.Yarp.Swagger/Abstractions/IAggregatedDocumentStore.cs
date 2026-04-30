using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Stores aggregated OpenAPI documents keyed by document name.
/// </summary>
public interface IAggregatedDocumentStore
{
    /// <summary>Returns a cached document, or <c>null</c> when not present.</summary>
    ValueTask<OpenApiDocument?> GetAsync(
        string documentName,
        CancellationToken cancellationToken = default);

    /// <summary>Stores the aggregated document, overwriting any previous value.</summary>
    ValueTask SetAsync(
        string documentName,
        OpenApiDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all currently cached document names.</summary>
    IReadOnlyList<string> GetDocumentNames();
}
