using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Options for <c>UseSwaggerAggregationDocuments</c>, the library-owned middleware that
/// serves <c>/{prefix}/{documentName}/swagger.json</c> with explicit 200 / 404 / 503
/// semantics rather than depending on Swashbuckle's default exception handling.
/// </summary>
public sealed class SwaggerAggregationDocumentEndpointOptions
{
    /// <summary>Route prefix the middleware listens on. Defaults to <c>/swagger</c>.</summary>
    public PathString RoutePrefix { get; set; } = "/swagger";

    /// <summary>HTTP status returned when a document is known but cannot be loaded.</summary>
    public int UnavailableStatusCode { get; set; } = StatusCodes.Status503ServiceUnavailable;

    /// <summary>
    /// Include a JSON body listing known document names when responding 404 to an unknown
    /// document. On by default; turn off if exposing the list is sensitive.
    /// </summary>
    public bool IncludeKnownDocumentsOnNotFound { get; set; } = true;

    /// <summary>
    /// OpenAPI specification version used when serializing aggregated documents. Microsoft.OpenApi
    /// 2.4.1 supports <see cref="OpenApiSpecVersion.OpenApi3_0"/> and
    /// <see cref="OpenApiSpecVersion.OpenApi3_1"/>; 3.2 is not yet emitted by the library
    /// regardless of this setting. The default is 3.0 because Swagger UI tooling has the
    /// widest interop there. Set to 3.1 if downstream services emit 3.1 and you want the
    /// aggregator to keep that wire format. Bumping per-document preservation lives behind
    /// custom mergers - this setting is global.
    /// </summary>
    public OpenApiSpecVersion OpenApiSpecVersion { get; set; } = OpenApiSpecVersion.OpenApi3_0;

    /// <summary>
    /// Document name used by the optional diagnostics endpoint (see
    /// <see cref="EmptySwaggerEndpointBehavior.DiagnosticEndpoint"/>). Reaching
    /// <c>/{RoutePrefix}/{DiagnosticsDocumentName}/swagger.json</c> returns a synthetic
    /// OpenAPI document whose <c>info.description</c> lists the current discovery
    /// diagnostics. Defaults to <c>"diagnostics"</c>.
    /// </summary>
    public string DiagnosticsDocumentName { get; set; } = "diagnostics";
}
