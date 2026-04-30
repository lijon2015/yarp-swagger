namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Strongly typed options for <c>ConfigureAggregatedEndpoints</c>. Replaces the old fake
/// <c>/swagger/v1/swagger.json</c> placeholder behavior - the UI now only advertises
/// documents that the discovery pipeline actually found.
/// </summary>
public sealed class AggregatedSwaggerUIOptions
{
    /// <summary>
    /// What to do when discovery returns zero documents. Defaults to
    /// <see cref="EmptySwaggerEndpointBehavior.NoEndpoints"/>.
    /// </summary>
    public EmptySwaggerEndpointBehavior EmptyBehavior { get; set; } =
        EmptySwaggerEndpointBehavior.NoEndpoints;

    /// <summary>
    /// Optional document name to use as <c>urls.primaryName</c>. When set and present in
    /// the discovered urls, Swagger UI selects this document by default.
    /// </summary>
    public string? PrimaryDocumentName { get; set; }

    /// <summary>
    /// Route prefix used when building document urls. Must match the route prefix used by
    /// <c>UseSwaggerAggregationDocuments</c>. Defaults to <c>/swagger</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "/swagger";

    /// <summary>
    /// Document name used for the diagnostics endpoint when
    /// <see cref="EmptySwaggerEndpointBehavior.DiagnosticEndpoint"/> is selected. Must match
    /// <see cref="SwaggerAggregationDocumentEndpointOptions.DiagnosticsDocumentName"/>.
    /// </summary>
    public string DiagnosticsDocumentName { get; set; } = "diagnostics";
}
