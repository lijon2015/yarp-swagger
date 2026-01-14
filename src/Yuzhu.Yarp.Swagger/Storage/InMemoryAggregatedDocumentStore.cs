using System.Collections.Concurrent;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Storage;

/// <summary>
/// 内存中的聚合文档存储
/// </summary>
public sealed class InMemoryAggregatedDocumentStore : IAggregatedDocumentStore
{
    private readonly ConcurrentDictionary<string, OpenApiDocument> _documents = new(StringComparer.OrdinalIgnoreCase);

    public OpenApiDocument? Get(string documentName)
    {
        _documents.TryGetValue(documentName, out var doc);
        return doc;
    }

    public ValueTask<OpenApiDocument?> GetAsync(string documentName, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Get(documentName));
    }

    public ValueTask SetAsync(string documentName, OpenApiDocument document, CancellationToken cancellationToken = default)
    {
        _documents[documentName] = document;
        return ValueTask.CompletedTask;
    }

    public bool Exists(string documentName)
    {
        return _documents.ContainsKey(documentName);
    }

    public IReadOnlyList<string> GetDocumentNames()
    {
        return _documents.Keys.ToList();
    }
}
