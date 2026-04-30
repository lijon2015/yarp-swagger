namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Reads metadata associated with a cluster candidate. Implementations must accept the
/// canonical flat key (for example <c>Swagger:Enabled</c>) regardless of whether the
/// underlying store represents the value as a flat key or a nested configuration path.
/// </summary>
public interface ISwaggerMetadataAccessor
{
    /// <summary>
    /// Read the metadata value for <paramref name="key"/>, returning <c>null</c> when absent.
    /// </summary>
    string? Get(string key);
}
