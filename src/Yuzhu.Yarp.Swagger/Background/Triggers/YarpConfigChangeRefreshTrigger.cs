using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Background.Triggers;

/// <summary>
/// Refresh trigger that fires when YARP's proxy configuration changes. Plugs into the
/// project's <see cref="IProxyConfigProvider"/> implementation so the aggregator picks up
/// dynamic destination changes (e.g. Consul -&gt; YARP) without waiting for the periodic
/// timer.
/// </summary>
public sealed class YarpConfigChangeRefreshTrigger(
    IProxyConfigProvider configProvider) : ISwaggerRefreshTrigger
{
    private readonly IProxyConfigProvider _configProvider = configProvider;

    public IChangeToken GetReloadToken() => _configProvider.GetConfig().ChangeToken;
}
