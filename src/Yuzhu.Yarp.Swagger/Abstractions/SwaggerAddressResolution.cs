namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Outcome of an <see cref="ISwaggerEndpointAddressResolver"/> attempt.
/// </summary>
/// <param name="Resolved">Whether this resolver produced a base address.</param>
/// <param name="BaseAddress">The resolved base address, or <c>null</c> when not resolved.</param>
/// <param name="SkippedReason">
/// Optional human-readable reason for not resolving. Surfaced in diagnostics when present.
/// </param>
public sealed record SwaggerAddressResolution(
    bool Resolved,
    Uri? BaseAddress,
    string? SkippedReason)
{
    /// <summary>The resolver could not handle this candidate; try the next resolver.</summary>
    public static SwaggerAddressResolution NotApplicable { get; } = new(false, null, null);

    /// <summary>The resolver applies to this candidate but had no usable destination.</summary>
    public static SwaggerAddressResolution Skipped(string reason) => new(false, null, reason);

    /// <summary>The resolver produced a usable base address.</summary>
    public static SwaggerAddressResolution Resolve(Uri baseAddress) => new(true, baseAddress, null);
}
