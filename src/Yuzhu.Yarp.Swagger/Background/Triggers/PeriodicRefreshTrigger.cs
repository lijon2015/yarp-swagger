using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Background.Triggers;

/// <summary>
/// Returns a token that fires after <see cref="SwaggerAggregationOptions.RefreshInterval"/>.
/// Acts as the long-term "fallback" refresh trigger. Event-driven triggers
/// (options change, YARP config change, project-specific Consul triggers) should drive most
/// refreshes; this just guarantees periodic reconciliation.
/// </summary>
public sealed class PeriodicRefreshTrigger(
    IOptionsMonitor<SwaggerAggregationOptions> options) : ISwaggerRefreshTrigger
{
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;

    public IChangeToken GetReloadToken()
    {
        TimeSpan interval = _options.CurrentValue.RefreshInterval;
        CancellationTokenSource cts = new(interval);
        return new CancellationChangeToken(cts.Token);
    }
}
