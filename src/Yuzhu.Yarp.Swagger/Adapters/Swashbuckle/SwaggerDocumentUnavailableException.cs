namespace Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;

/// <summary>
/// Thrown when a known aggregated Swagger document cannot be loaded successfully.
/// </summary>
public sealed class SwaggerDocumentUnavailableException : InvalidOperationException
{
    public SwaggerDocumentUnavailableException(string documentName)
        : base($"Swagger document '{documentName}' is currently unavailable.")
    {
        DocumentName = documentName;
    }

    public string DocumentName { get; }
}
