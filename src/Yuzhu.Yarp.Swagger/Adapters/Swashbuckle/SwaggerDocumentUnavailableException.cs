namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Thrown when a known aggregated Swagger document cannot be loaded successfully. The
/// library-owned middleware translates this into a structured 5xx response; Swashbuckle's
/// <c>UseSwagger()</c> middleware would otherwise produce an opaque 500.
/// </summary>
public sealed class SwaggerDocumentUnavailableException(
    string documentName,
    string? reason = null)
    : InvalidOperationException(
        $"Swagger document '{documentName}' is currently unavailable" +
        (reason is null ? "." : $": {reason}"))
{
    /// <summary>The document name that failed.</summary>
    public string DocumentName { get; } = documentName;

    /// <summary>Optional reason from the aggregator.</summary>
    public string? Reason { get; } = reason;
}
