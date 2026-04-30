using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Loads a single Swagger document from its <see cref="SwaggerEndpoint"/>.
/// </summary>
public interface ISwaggerDocumentLoader
{
    /// <summary>Loads the document for the endpoint.</summary>
    Task<SwaggerLoadResult> LoadAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a document load attempt.
/// </summary>
public sealed record SwaggerLoadResult
{
    /// <summary>The endpoint that was loaded.</summary>
    public required SwaggerEndpoint Endpoint { get; init; }

    /// <summary>Loaded document on success; <c>null</c> on failure.</summary>
    public OpenApiDocument? Document { get; init; }

    /// <summary><c>true</c> when <see cref="Document"/> is non-null.</summary>
    public bool IsSuccess => Document is not null;

    /// <summary>HTTP status code when the load reached the network, or <c>null</c>.</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>Stage where the load failed (<c>http</c>, <c>parse</c>, <c>size</c>, <c>timeout</c>, <c>token</c>).</summary>
    public string? FailureStage { get; init; }

    /// <summary>Free-form error description.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Wall-clock duration of the load attempt.</summary>
    public TimeSpan LoadDuration { get; init; }
}
