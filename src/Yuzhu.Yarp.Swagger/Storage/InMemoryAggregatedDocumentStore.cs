using System.Collections.Concurrent;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Storage;

/// <summary>
/// Default in-memory store for aggregated documents.
/// </summary>
public sealed class InMemoryAggregatedDocumentStore : IAggregatedDocumentStore
{
    private readonly ConcurrentDictionary<string, OpenApiDocument> _documents =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask<OpenApiDocument?> GetAsync(
        string documentName,
        CancellationToken cancellationToken = default)
    {
        _ = _documents.TryGetValue(documentName, out OpenApiDocument? document);
        return ValueTask.FromResult(document);
    }

    public ValueTask SetAsync(
        string documentName,
        OpenApiDocument document,
        CancellationToken cancellationToken = default)
    {
        _documents[documentName] = document;
        return ValueTask.CompletedTask;
    }

    public IReadOnlyList<string> GetDocumentNames() => [.. _documents.Keys];
}
