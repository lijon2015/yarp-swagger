using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Configures Swagger UI's <c>urls</c> dropdown from the discovery service. The previous
/// behavior of registering a fake <c>/swagger/v1/swagger.json</c> placeholder is gone -
/// when discovery returns zero documents, the UI gets an empty url list (or a clearly
/// labeled diagnostic endpoint, depending on
/// <see cref="AggregatedSwaggerUIOptions.EmptyBehavior"/>).
/// </summary>
public static class SwaggerUIConfigurationExtensions
{
    private const string DiagnosticEndpointName = "Swagger Aggregation Diagnostics";

    /// <summary>
    /// Synchronous overload for use inside <c>app.UseSwaggerUI(o =&gt; ...)</c>. Resolves the
    /// current discovery snapshot from DI and configures urls accordingly.
    /// </summary>
    public static SwaggerUIOptions ConfigureAggregatedEndpoints(
        this SwaggerUIOptions options,
        IServiceProvider serviceProvider,
        Action<AggregatedSwaggerUIOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        AggregatedSwaggerUIOptions uiOptions = new();
        // Adopt the document-endpoint defaults so the UI's diagnostics URL matches the
        // middleware's diagnostics route without the caller having to keep them in sync.
        SwaggerAggregationDocumentEndpointOptions middlewareDefaults =
            serviceProvider.GetService<IOptions<SwaggerAggregationDocumentEndpointOptions>>()
                ?.Value ?? new SwaggerAggregationDocumentEndpointOptions();
        uiOptions.RoutePrefix = middlewareDefaults.RoutePrefix.Value ?? "/swagger";
        uiOptions.DiagnosticsDocumentName = middlewareDefaults.DiagnosticsDocumentName;

        configure?.Invoke(uiOptions);

        ISwaggerEndpointDiscoveryService discovery =
            serviceProvider.GetRequiredService<ISwaggerEndpointDiscoveryService>();
        SwaggerDocumentCoordinator coordinator =
            serviceProvider.GetRequiredService<SwaggerDocumentCoordinator>();
        ILogger logger =
            serviceProvider.GetService<ILogger<AggregatedSwaggerUIOptions>>()
            ?? NullLogger<AggregatedSwaggerUIOptions>.Instance;

        IReadOnlyList<string> documentNames = ResolveDocumentNames(discovery, coordinator);
        ApplyUrls(options, uiOptions, documentNames, logger);

        return options;
    }

    private static IReadOnlyList<string> ResolveDocumentNames(
        ISwaggerEndpointDiscoveryService discovery,
        SwaggerDocumentCoordinator coordinator)
    {
        // Prefer cached document names (last successful refresh); fall back to a fresh
        // discovery if nothing's been refreshed yet.
        ValueTask<IReadOnlyList<string>> namesTask = coordinator.GetDocumentNamesAsync();
        IReadOnlyList<string> names = namesTask.IsCompletedSuccessfully
            ? namesTask.Result
            : namesTask.AsTask().GetAwaiter().GetResult();

        if (names.Count > 0)
        {
            return names;
        }

        ValueTask<SwaggerEndpointDiscoveryResult> discoveryTask = discovery.DiscoverAsync();
        SwaggerEndpointDiscoveryResult result = discoveryTask.IsCompletedSuccessfully
            ? discoveryTask.Result
            : discoveryTask.AsTask().GetAwaiter().GetResult();

        return [.. result.Endpoints
            .Select(static e => e.DocumentName)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void ApplyUrls(
        SwaggerUIOptions options,
        AggregatedSwaggerUIOptions uiOptions,
        IReadOnlyList<string> documentNames,
        ILogger logger)
    {
        string prefix = NormalizePrefix(uiOptions.RoutePrefix);

        if (documentNames.Count == 0)
        {
            switch (uiOptions.EmptyBehavior)
            {
                case EmptySwaggerEndpointBehavior.NoEndpoints:
                    logger.LogWarning(
                        "Swagger aggregation produced 0 documents; UI dropdown will be empty");
                    options.ConfigObject.Urls = [];
                    return;

                case EmptySwaggerEndpointBehavior.DiagnosticEndpoint:
                    logger.LogWarning(
                        "Swagger aggregation produced 0 documents; advertising diagnostic endpoint only");
                    options.ConfigObject.Urls = [
                        new UrlDescriptor
                        {
                            Url = BuildDocumentUrl(prefix, uiOptions.DiagnosticsDocumentName),
                            Name = DiagnosticEndpointName,
                        },
                    ];
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported {nameof(EmptySwaggerEndpointBehavior)}: {uiOptions.EmptyBehavior}");
            }
        }

        List<UrlDescriptor> urls = [];
        foreach (string documentName in documentNames)
        {
            urls.Add(new UrlDescriptor
            {
                Url = BuildDocumentUrl(prefix, documentName),
                Name = documentName,
            });
        }

        options.ConfigObject.Urls = urls;

        if (!string.IsNullOrEmpty(uiOptions.PrimaryDocumentName)
            && documentNames.Any(n => string.Equals(n, uiOptions.PrimaryDocumentName, StringComparison.OrdinalIgnoreCase)))
        {
            options.ConfigObject.AdditionalItems["urls.primaryName"] = uiOptions.PrimaryDocumentName;
        }
    }

    private static string BuildDocumentUrl(string prefix, string documentName) =>
        $"{prefix}/{documentName}/swagger.json";

    private static string NormalizePrefix(string prefix) =>
        string.IsNullOrEmpty(prefix)
            ? string.Empty
            : prefix.StartsWith('/') ? prefix.TrimEnd('/') : "/" + prefix.TrimEnd('/');
}
