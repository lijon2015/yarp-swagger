using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Discovery.Metadata;

/// <summary>
/// Reads metadata from a flat <see cref="IReadOnlyDictionary{TKey, TValue}"/> keyed by the
/// canonical <c>Swagger:Foo</c> form. Used by <c>YarpRuntimeSwaggerEndpointSource</c> where
/// YARP exposes <c>ClusterConfig.Metadata</c> as a flat string dictionary.
/// </summary>
public sealed class DictionarySwaggerMetadataAccessor(
    IReadOnlyDictionary<string, string>? metadata) : ISwaggerMetadataAccessor
{
    /// <summary>Empty accessor that returns <c>null</c> for every key.</summary>
    public static ISwaggerMetadataAccessor Empty { get; } = new DictionarySwaggerMetadataAccessor(null);

    private readonly IReadOnlyDictionary<string, string>? _metadata = metadata;

    public string? Get(string key) =>
        _metadata is not null && _metadata.TryGetValue(key, out string? value) ? value : null;
}
