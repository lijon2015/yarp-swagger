using Microsoft.Extensions.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Discovery.Metadata;

/// <summary>
/// Reads metadata from an <see cref="IConfigurationSection"/> rooted at the cluster's
/// <c>Metadata</c> node. Supports both the flat key form (<c>"Swagger:Enabled"</c>) and the
/// nested form (<c>Swagger:Enabled</c> resolved as the path <c>Swagger -&gt; Enabled</c>).
/// JSON configuration sources expose nested forms; environment / .NET keyed providers expose
/// the flat form. Both must work, per the long-term plan.
/// </summary>
public sealed class ConfigurationSwaggerMetadataAccessor(
    IConfiguration metadataSection) : ISwaggerMetadataAccessor
{
    private readonly IConfiguration _metadataSection = metadataSection;

    public string? Get(string key)
    {
        // Direct flat-key lookup first (e.g. environment variable provider with
        // double-underscore separator).
        string? direct = _metadataSection[key];
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        // Then nested path lookup (e.g. JSON / YAML configuration sources).
        string? nested = _metadataSection.GetSection(key).Value;
        return string.IsNullOrEmpty(nested) ? null : nested;
    }
}
