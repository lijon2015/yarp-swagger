using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Transforming;

/// <summary>
/// 路径前缀转换器
/// </summary>
public sealed class PathPrefixTransformer : ISwaggerDocumentTransformer
{
    public int Order => 0;

    public ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        string? prefix = context.Endpoint.PathPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            return ValueTask.FromResult(document);
        }

        OpenApiPaths newPaths = [];
        string normalizedPrefix = prefix.TrimEnd('/');

        foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
        {
            string normalizedPath = path.Key.StartsWith('/') ? path.Key : "/" + path.Key;
            string newKey = normalizedPrefix + normalizedPath;
            newPaths[newKey] = path.Value;
        }

        document.Paths = newPaths;

        return ValueTask.FromResult(document);
    }
}
