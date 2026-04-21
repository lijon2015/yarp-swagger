namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Thrown when a known aggregated Swagger document cannot be loaded successfully.
/// </summary>
public sealed class SwaggerDocumentUnavailableException(string documentName) : InvalidOperationException($"Swagger document '{documentName}' is currently unavailable.")
{
    public string DocumentName { get; } = documentName;
}
