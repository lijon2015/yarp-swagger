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
        var prefix = context.Endpoint.PathPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            return ValueTask.FromResult(document);
        }

        var newPaths = new OpenApiPaths();
        var normalizedPrefix = prefix.TrimEnd('/');

        foreach (var path in document.Paths)
        {
            var normalizedPath = path.Key.StartsWith('/') ? path.Key : "/" + path.Key;
            var newKey = normalizedPrefix + normalizedPath;
            newPaths[newKey] = path.Value;
        }

        document.Paths = newPaths;

        return ValueTask.FromResult(document);
    }
}
