using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Merging;

/// <summary>
/// Default merger. The first endpoint flagged <see cref="SwaggerEndpoint.IsMetadataSource"/>
/// wins for <see cref="OpenApiDocument.Info"/>; otherwise the merged document falls back to
/// a generic title using the document name. Paths and components merge first-wins.
/// </summary>
public sealed class DefaultSwaggerDocumentMerger(ILogger<DefaultSwaggerDocumentMerger> logger) : ISwaggerDocumentMerger
{
    private readonly ILogger<DefaultSwaggerDocumentMerger> _logger = logger;

    public OpenApiDocument Merge(
        string documentName,
        IReadOnlyList<SwaggerLoadResult> sources,
        SwaggerMergeOptions options)
    {
        OpenApiDocument result = new()
        {
            Info = new OpenApiInfo
            {
                Title = documentName,
                Version = "1.0.0",
            },
            Paths = [],
            Components = new OpenApiComponents(),
        };

        List<string> failures = [];
        List<OpenApiSecurityRequirement> security = [];
        Dictionary<string, OpenApiTag> tagsByName = new(StringComparer.Ordinal);
        bool metadataSourceApplied = false;

        foreach (SwaggerLoadResult source in sources)
        {
            if (!source.IsSuccess || source.Document is null)
            {
                failures.Add($"{source.Endpoint.ClusterId} ({source.ErrorMessage ?? "unknown"})");
                continue;
            }

            OpenApiDocument document = source.Document;
            SwaggerEndpoint endpoint = source.Endpoint;

            if (!metadataSourceApplied && endpoint.IsMetadataSource && document.Info is not null)
            {
                result.Info = document.Info;
                metadataSourceApplied = true;
            }

            MergeComponents(result.Components, document.Components, endpoint.ClusterId);
            MergePaths(result.Paths, document.Paths, endpoint.ClusterId);

            if (document.Security is { Count: > 0 })
            {
                security.AddRange(document.Security);
            }

            if (document.Tags is not null)
            {
                foreach (OpenApiTag tag in document.Tags)
                {
                    if (string.IsNullOrWhiteSpace(tag.Name))
                    {
                        continue;
                    }

                    _ = tagsByName.TryAdd(tag.Name, tag);
                }
            }
        }

        result.Security = security;
        result.Tags = new HashSet<OpenApiTag>(tagsByName.Values);

        if (options.IncludeFailedServicesWarning && failures.Count > 0)
        {
            string suffix = $"\n\n**Warning**: failed to load Swagger for: {string.Join(", ", failures)}";
            result.Info.Description = (result.Info.Description ?? string.Empty) + suffix;
        }

        if (failures.Count > 0)
        {
            _logger.LogWarning(
                "Aggregation of '{DocumentName}' completed with {FailedCount} failure(s): {Services}",
                documentName,
                failures.Count,
                string.Join(", ", failures));
        }

        return result;
    }

    private void MergePaths(OpenApiPaths target, OpenApiPaths? source, string clusterId)
    {
        if (source is null)
        {
            return;
        }

        foreach (KeyValuePair<string, IOpenApiPathItem> entry in source)
        {
            if (!target.TryAdd(entry.Key, (OpenApiPathItem)entry.Value))
            {
                _logger.LogDebug(
                    "Path conflict on '{Path}' from {ClusterId}; keeping first definition",
                    entry.Key,
                    clusterId);
            }
        }
    }

    private void MergeComponents(OpenApiComponents target, OpenApiComponents? source, string clusterId)
    {
        if (source is null)
        {
            return;
        }

        MergeDictionary(target.Schemas ??= new Dictionary<string, IOpenApiSchema>(), source.Schemas, clusterId, "Schema");
        MergeDictionary(target.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(), source.SecuritySchemes, clusterId, "SecurityScheme");
        MergeDictionary(target.Parameters ??= new Dictionary<string, IOpenApiParameter>(), source.Parameters, clusterId, "Parameter");
        MergeDictionary(target.Responses ??= new Dictionary<string, IOpenApiResponse>(), source.Responses, clusterId, "Response");
        MergeDictionary(target.RequestBodies ??= new Dictionary<string, IOpenApiRequestBody>(), source.RequestBodies, clusterId, "RequestBody");
        MergeDictionary(target.Headers ??= new Dictionary<string, IOpenApiHeader>(), source.Headers, clusterId, "Header");
        MergeDictionary(target.Examples ??= new Dictionary<string, IOpenApiExample>(), source.Examples, clusterId, "Example");
        MergeDictionary(target.Links ??= new Dictionary<string, IOpenApiLink>(), source.Links, clusterId, "Link");
        MergeDictionary(target.Callbacks ??= new Dictionary<string, IOpenApiCallback>(), source.Callbacks, clusterId, "Callback");

        if (source.Extensions is not null)
        {
            target.Extensions ??= new Dictionary<string, IOpenApiExtension>();
            foreach (KeyValuePair<string, IOpenApiExtension> entry in source.Extensions)
            {
                target.Extensions[entry.Key] = entry.Value;
            }
        }
    }

    private void MergeDictionary<T>(
        IDictionary<string, T> target,
        IDictionary<string, T>? source,
        string clusterId,
        string componentType)
    {
        if (source is null)
        {
            return;
        }

        foreach (KeyValuePair<string, T> entry in source)
        {
            if (!target.TryAdd(entry.Key, entry.Value))
            {
                _logger.LogDebug(
                    "{ComponentType} conflict on '{Key}' from {ClusterId}; keeping first definition",
                    componentType,
                    entry.Key,
                    clusterId);
            }
        }
    }
}
