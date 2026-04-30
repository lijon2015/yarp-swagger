using Microsoft.OpenApi;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// A single stage in the per-document transformation pipeline.
/// </summary>
public interface ISwaggerDocumentTransformer
{
    /// <summary>Lower order runs first.</summary>
    int Order => 0;

    /// <summary>Transform <paramref name="document"/> in-place or return a replacement.</summary>
    ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context passed to a <see cref="ISwaggerDocumentTransformer"/>.
/// </summary>
public sealed record TransformContext
{
    /// <summary>YARP cluster id for the source document.</summary>
    public required string ClusterId { get; init; }

    /// <summary>The discovered endpoint.</summary>
    public required SwaggerEndpoint Endpoint { get; init; }
}
