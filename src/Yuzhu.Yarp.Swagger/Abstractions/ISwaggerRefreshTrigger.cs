using Microsoft.Extensions.Primitives;

namespace Yuzhu.Yarp.Swagger.Abstractions;

/// <summary>
/// Provides change tokens that fire when the aggregated documents should be refreshed.
/// Multiple triggers may be registered; the refresh service composes them.
/// </summary>
public interface ISwaggerRefreshTrigger
{
    /// <summary>
    /// Returns the current change token. The refresh service registers a callback on the
    /// token, runs a refresh when it fires, and asks for a new token afterwards. Returning a
    /// token that never fires is valid (the trigger is dormant).
    /// </summary>
    IChangeToken GetReloadToken();
}
