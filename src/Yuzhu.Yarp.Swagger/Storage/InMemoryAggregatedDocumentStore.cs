using Microsoft.OpenApi;
using System.Collections.Concurrent;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Storage;

/// <summary>
/// In-memory store for aggregated OpenAPI documents.
/// </summary>
public sealed class InMemoryAggregatedDocumentStore : IAggregatedDocumentStore
{
    private readonly ConcurrentDictionary<string, OpenApiDocument> _documents = new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<OpenApiDocument?> GetAsync(string documentName, CancellationToken cancellationToken = default)
    {
        _ = _documents.TryGetValue(documentName, out OpenApiDocument? doc);
        return ValueTask.FromResult(doc);
    }

    public ValueTask SetAsync(string documentName, OpenApiDocument document, CancellationToken cancellationToken = default)
    {
        _documents[documentName] = document;
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<string> GetDocumentNames() => [.. _documents.Keys];
}
