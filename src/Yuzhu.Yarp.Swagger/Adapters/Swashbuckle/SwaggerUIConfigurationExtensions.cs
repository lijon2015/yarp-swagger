using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerUI;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Swagger UI configuration helpers.
/// </summary>
public static class SwaggerUIConfigurationExtensions
{
    /// <summary>
    /// Configure Swagger UI endpoints from the registered endpoint provider.
    /// </summary>
    public static void ConfigureAggregatedEndpoints(
        this SwaggerUIOptions options,
        IServiceProvider serviceProvider)
    {
        var endpointProvider = serviceProvider.GetRequiredService<ISwaggerEndpointProvider>();

        var urls = endpointProvider.GetEndpoints()
            .Select(endpoint => endpoint.DocumentName ?? endpoint.ClusterId)
            .Where(static documentName => !string.IsNullOrWhiteSpace(documentName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(documentName => new UrlDescriptor
            {
                Url = string.Format(SwaggerConstants.SwaggerJsonPathTemplate, documentName),
                Name = documentName
            })
            .ToList();

        if (urls.Count == 0)
        {
            urls.Add(new UrlDescriptor
            {
                Url = "/swagger/v1/swagger.json",
                Name = "API (Loading...)"
            });
        }

        options.ConfigObject.Urls = urls;
    }
}
