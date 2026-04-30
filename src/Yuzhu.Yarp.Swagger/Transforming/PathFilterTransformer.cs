using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Transforming;

/// <summary>
/// Drops any document path that doesn't match <see cref="SwaggerEndpoint.PathFilter"/>.
/// The pattern is compiled with a hard timeout to defend against ReDoS, and the compiled
/// instance is cached across calls so repeated refreshes don't re-compile the same regex.
/// </summary>
public sealed class PathFilterTransformer(ILogger<PathFilterTransformer> logger) : ISwaggerDocumentTransformer
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();
    private readonly ILogger<PathFilterTransformer> _logger = logger;

    public int Order => 10;

    public ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        string? pattern = context.Endpoint.PathFilter;
        if (string.IsNullOrEmpty(pattern))
        {
            return ValueTask.FromResult(document);
        }

        if (pattern.Length > SwaggerConstants.MaxPathFilterLength)
        {
            _logger.LogWarning(
                "Path filter pattern too long for {ClusterId}: {Length} > {MaxLength}",
                context.ClusterId,
                pattern.Length,
                SwaggerConstants.MaxPathFilterLength);
            return ValueTask.FromResult(document);
        }

        Regex regex;
        try
        {
            regex = RegexCache.GetOrAdd(pattern, static p =>
                new Regex(p, RegexOptions.Compiled, SwaggerConstants.RegexTimeout));
        }
        catch (RegexParseException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid path filter regex for {ClusterId}: {Pattern}",
                context.ClusterId,
                pattern);
            return ValueTask.FromResult(document);
        }

        OpenApiPaths kept = [];
        foreach (KeyValuePair<string, IOpenApiPathItem> path in document.Paths)
        {
            try
            {
                if (regex.IsMatch(path.Key))
                {
                    kept[path.Key] = path.Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning(
                    "Regex match timeout for path {Path} in {ClusterId}",
                    path.Key,
                    context.ClusterId);
            }
        }

        document.Paths = kept;
        return ValueTask.FromResult(document);
    }
}
