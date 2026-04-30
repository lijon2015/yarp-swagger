using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Transforming;

/// <summary>
/// Prepends <see cref="SwaggerEndpoint.PathPrefix"/> to every path in the document. This
/// matches the rewrite YARP applies to the request, so consumers of the merged document
/// see paths that match the gateway-facing URL.
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

        string normalizedPrefix = prefix.TrimEnd('/');
        OpenApiPaths rewritten = [];

        foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
        {
            string normalizedPath = path.Key.StartsWith('/') ? path.Key : "/" + path.Key;
            rewritten[normalizedPrefix + normalizedPath] = path.Value;
        }

        document.Paths = rewritten;
        return ValueTask.FromResult(document);
    }
}
