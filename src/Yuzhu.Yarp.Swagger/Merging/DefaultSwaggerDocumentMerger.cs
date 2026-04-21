using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Merging;

/// <summary>
/// Default swagger document merger.
/// </summary>
public sealed class DefaultSwaggerDocumentMerger : ISwaggerDocumentMerger
{
    private readonly ILogger<DefaultSwaggerDocumentMerger> _logger;

    public DefaultSwaggerDocumentMerger(ILogger<DefaultSwaggerDocumentMerger> logger)
    {
        _logger = logger;
    }

    public OpenApiDocument Merge(
        IEnumerable<SwaggerLoadResult> sources,
        MergeOptions options)
    {
        var resultDocument = new OpenApiDocument
        {
            Info = new OpenApiInfo
            {
                Title = "Aggregated API",
                Version = "1.0.0"
            },
            Paths = new OpenApiPaths(),
            Components = new OpenApiComponents()
        };

        var failedServices = new List<string>();
        var securityRequirements = new List<OpenApiSecurityRequirement>();
        var tagsByName = new Dictionary<string, OpenApiTag>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            if (!source.IsSuccess)
            {
                failedServices.Add($"{source.Endpoint.ClusterId} ({source.ErrorMessage})");
                continue;
            }

            var document = source.Document!;
            var endpoint = source.Endpoint;

            if (endpoint.IsMetadataSource)
            {
                resultDocument.Info = document.Info;
            }

            MergeComponents(resultDocument.Components, document.Components, endpoint, _logger);
            MergePaths(resultDocument.Paths, document.Paths, endpoint, _logger);

            if (document.Security != null)
            {
                securityRequirements.AddRange(document.Security);
            }

            if (document.Tags != null)
            {
                foreach (var tag in document.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag.Name))
                    {
                        _logger.LogDebug(
                            "Skipping unnamed tag from {ClusterId}",
                            endpoint.ClusterId);
                        continue;
                    }

                    if (!tagsByName.TryAdd(tag.Name, tag))
                    {
                        _logger.LogDebug(
                            "Tag {TagName} from {ClusterId} already exists, keeping the first definition",
                            tag.Name,
                            endpoint.ClusterId);
                    }
                }
            }
        }

        resultDocument.Security = securityRequirements;
        resultDocument.Tags = new HashSet<OpenApiTag>(tagsByName.Values);

        if (failedServices.Count > 0 && options.IncludeFailedServicesWarning)
        {
            resultDocument.Info.Description =
                (resultDocument.Info.Description ?? "") +
                $"\n\n**Warning**: Failed to load Swagger for: {string.Join(", ", failedServices)}";

            _logger.LogWarning(
                "Aggregation completed with failures: {Services}",
                string.Join(", ", failedServices));
        }

        return resultDocument;
    }

    private static void MergePaths(
        OpenApiPaths targetPaths,
        OpenApiPaths? sourcePaths,
        SwaggerEndpoint endpoint,
        ILogger logger)
    {
        if (sourcePaths == null)
        {
            return;
        }

        foreach (var path in sourcePaths)
        {
            if (!targetPaths.TryAdd(path.Key, (OpenApiPathItem)path.Value))
            {
                logger.LogDebug(
                    "Path {Path} from {ClusterId} already exists, skipping",
                    path.Key,
                    endpoint.ClusterId);
            }
        }
    }

    private static void MergeComponents(
        OpenApiComponents targetComponents,
        OpenApiComponents? sourceComponents,
        SwaggerEndpoint endpoint,
        ILogger logger)
    {
        if (sourceComponents == null)
        {
            return;
        }

        MergeDictionary(
            targetComponents.Schemas ??= new Dictionary<string, IOpenApiSchema>(),
            sourceComponents.Schemas,
            endpoint.ClusterId,
            "Schema",
            logger);

        MergeDictionary(
            targetComponents.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(),
            sourceComponents.SecuritySchemes,
            endpoint.ClusterId,
            "SecurityScheme",
            logger);

        MergeDictionary(
            targetComponents.Parameters ??= new Dictionary<string, IOpenApiParameter>(),
            sourceComponents.Parameters,
            endpoint.ClusterId,
            "Parameter",
            logger);

        MergeDictionary(
            targetComponents.Responses ??= new Dictionary<string, IOpenApiResponse>(),
            sourceComponents.Responses,
            endpoint.ClusterId,
            "Response",
            logger);

        MergeDictionary(
            targetComponents.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(),
            sourceComponents.RequestBodies,
            endpoint.ClusterId,
            "RequestBody",
            logger);

        MergeDictionary(
            targetComponents.Headers ??= new Dictionary<string, IOpenApiHeader>(),
            sourceComponents.Headers,
            endpoint.ClusterId,
            "Header",
            logger);

        MergeDictionary(
            targetComponents.Examples ??= new Dictionary<string, IOpenApiExample>(),
            sourceComponents.Examples,
            endpoint.ClusterId,
            "Example",
            logger);

        MergeDictionary(
            targetComponents.Links ??= new Dictionary<string, IOpenApiLink>(),
            sourceComponents.Links,
            endpoint.ClusterId,
            "Link",
            logger);

        MergeDictionary(
            targetComponents.Callbacks ??= new Dictionary<string, IOpenApiCallback>(),
            sourceComponents.Callbacks,
            endpoint.ClusterId,
            "Callback",
            logger);

        if (sourceComponents.Extensions != null)
        {
            targetComponents.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            foreach (var ext in sourceComponents.Extensions)
            {
                targetComponents.Extensions[ext.Key] = ext.Value;
            }
        }
    }

    private static void MergeDictionary<T>(
        IDictionary<string, T> target,
        IDictionary<string, T>? source,
        string clusterId,
        string componentType,
        ILogger logger)
    {
        if (source == null)
        {
            return;
        }

        foreach (var item in source)
        {
            if (!target.TryAdd(item.Key, item.Value))
            {
                logger.LogDebug(
                    "{ComponentType} conflict for {Key} from {ClusterId}. Ignored (First wins).",
                    componentType,
                    item.Key,
                    clusterId);
            }
        }
    }
}
