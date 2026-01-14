using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Transforming;

/// <summary>
/// 路径过滤转换器
/// </summary>
public sealed class PathFilterTransformer : ISwaggerDocumentTransformer
{
    private readonly ILogger<PathFilterTransformer> _logger;

    /// <summary>
    /// 正则表达式缓存，避免重复编译
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    public PathFilterTransformer(ILogger<PathFilterTransformer> logger)
    {
        _logger = logger;
    }

    public int Order => 10;

    public ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        var filterPattern = context.Endpoint.PathFilter;

        if (string.IsNullOrEmpty(filterPattern))
        {
            return ValueTask.FromResult(document);
        }

        // 验证正则表达式长度
        if (filterPattern.Length > SwaggerConstants.MaxPathFilterLength)
        {
            _logger.LogWarning(
                "Path filter pattern too long for {ClusterId}: {Length} > {MaxLength}",
                context.ClusterId, filterPattern.Length, SwaggerConstants.MaxPathFilterLength);
            return ValueTask.FromResult(document);
        }

        Regex filterRegex;
        try
        {
            filterRegex = GetOrCreateRegex(filterPattern);
        }
        catch (RegexParseException ex)
        {
            _logger.LogWarning(ex,
                "Invalid path filter regex for {ClusterId}: {Pattern}",
                context.ClusterId, filterPattern);
            return ValueTask.FromResult(document);
        }

        var newPaths = new OpenApiPaths();

        foreach (var path in document.Paths)
        {
            try
            {
                if (filterRegex.IsMatch(path.Key))
                {
                    newPaths[path.Key] = path.Value;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning(
                    "Regex match timeout for path {Path} in {ClusterId}",
                    path.Key, context.ClusterId);
            }
        }

        document.Paths = newPaths;

        return ValueTask.FromResult(document);
    }

    /// <summary>
    /// 获取或创建缓存的正则表达式（带超时保护）
    /// </summary>
    private static Regex GetOrCreateRegex(string pattern)
    {
        return RegexCache.GetOrAdd(pattern, p =>
            new Regex(p, RegexOptions.Compiled, SwaggerConstants.RegexTimeout));
    }
}
