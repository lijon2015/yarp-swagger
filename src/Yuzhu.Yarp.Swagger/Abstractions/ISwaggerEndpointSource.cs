namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Emits cluster candidates for the discovery pipeline. Multiple sources can be registered;
/// candidates are deduplicated by <see cref="SwaggerClusterCandidate.ClusterId"/> with the
/// first source winning (registration order).
/// </summary>
public interface ISwaggerEndpointSource
{
    /// <summary>
    /// Returns the candidates this source currently knows about.
    /// </summary>
    ValueTask<IReadOnlyList<SwaggerClusterCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken = default);
}
