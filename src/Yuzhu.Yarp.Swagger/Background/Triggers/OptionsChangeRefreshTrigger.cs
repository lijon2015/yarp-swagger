using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Background.Triggers;

/// <summary>
/// Fires when <see cref="SwaggerAggregationOptions"/> change. Wires
/// <see cref="IOptionsMonitor{TOptions}.OnChange(Action{TOptions})"/> to a
/// <see cref="CancellationChangeToken"/> so refreshes pick up new tunables immediately.
/// </summary>
public sealed class OptionsChangeRefreshTrigger : ISwaggerRefreshTrigger, IDisposable
{
    private readonly Lock _gate = new();
    private readonly IDisposable? _registration;
    private CancellationTokenSource _cts = new();

    public OptionsChangeRefreshTrigger(IOptionsMonitor<SwaggerAggregationOptions> monitor)
    {
        _registration = monitor.OnChange(_ => Trigger());
    }

    public IChangeToken GetReloadToken()
    {
        lock (_gate)
        {
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }

            return new CancellationChangeToken(_cts.Token);
        }
    }

    private void Trigger()
    {
        CancellationTokenSource toCancel;
        lock (_gate)
        {
            toCancel = _cts;
        }

        try
        {
            toCancel.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _registration?.Dispose();
        lock (_gate)
        {
            _cts.Dispose();
        }
    }
}
