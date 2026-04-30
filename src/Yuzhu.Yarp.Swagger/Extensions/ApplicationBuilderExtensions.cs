using Microsoft.AspNetCore.Builder;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

namespace Yuzhu.Yarp.Swagger.Extensions;

/// <summary>
/// Pipeline registration helpers.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Register the library-owned aggregation document middleware. Must run before
    /// <c>UseSwagger()</c> so its 404 / 503 / 200 semantics take precedence over
    /// Swashbuckle's default exception handling. Configure route prefix and unavailable
    /// status via <c>services.PostConfigure&lt;SwaggerAggregationDocumentEndpointOptions&gt;</c>
    /// during DI registration.
    /// </summary>
    public static IApplicationBuilder UseSwaggerAggregationDocuments(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AggregatedSwaggerEndpointMiddleware>();
    }
}
