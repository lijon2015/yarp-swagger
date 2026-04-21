using System.Collections.Concurrent;
using Microsoft.OpenApi;
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
        _documents.TryGetValue(documentName, out var doc);
        return ValueTask.FromResult<OpenApiDocument?>(doc);
    }

    public ValueTask SetAsync(string documentName, OpenApiDocument document, CancellationToken cancellationToken = default)
    {
        _documents[documentName] = document;
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<string> GetDocumentNames()
    {
        return _documents.Keys.ToList();
    }
}
