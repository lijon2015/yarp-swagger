using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// Periodically refreshes aggregated Swagger documents in the background.
/// </summary>
public sealed class SwaggerRefreshService : BackgroundService
{
    private readonly SwaggerDocumentCoordinator _documentCoordinator;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options;
    private readonly ILogger<SwaggerRefreshService> _logger;
    private readonly IDisposable? _optionsChangeRegistration;

    private CancellationTokenSource? _configChangeCts;
    private readonly Lock _ctsLock = new();

    public SwaggerRefreshService(
        SwaggerDocumentCoordinator documentCoordinator,
        IOptionsMonitor<SwaggerAggregationOptions> options,
        ILogger<SwaggerRefreshService> logger)
    {
        _documentCoordinator = documentCoordinator;
        _options = options;
        _logger = logger;

        _optionsChangeRegistration = _options.OnChange(_ =>
        {
            _logger.LogInformation("Configuration changed, triggering refresh");
            TriggerRefresh();
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = _options.CurrentValue.StartupDelay;
        _logger.LogInformation(
            "Swagger refresh service starting, waiting {Delay} for YARP initialization",
            startupDelay);

        await Task.Delay(startupDelay, stoppingToken);

        _logger.LogInformation("Swagger refresh service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllDocumentsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during swagger refresh");
            }

            try
            {
                CancellationTokenSource configCts;
                lock (_ctsLock)
                {
                    _configChangeCts?.Dispose();
                    _configChangeCts = new CancellationTokenSource();
                    configCts = _configChangeCts;
                }

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    stoppingToken,
                    configCts.Token);

                await Task.Delay(_options.CurrentValue.RefreshInterval, linkedCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Refresh triggered by configuration change");
            }
        }
    }

    public void TriggerRefresh()
    {
        lock (_ctsLock)
        {
            try
            {
                _configChangeCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public override void Dispose()
    {
        _optionsChangeRegistration?.Dispose();

        lock (_ctsLock)
        {
            _configChangeCts?.Dispose();
            _configChangeCts = null;
        }

        base.Dispose();
    }

    private async Task RefreshAllDocumentsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var activity = SwaggerTelemetry.ActivitySource.StartActivity("RefreshAllDocuments");

        var result = await _documentCoordinator.RefreshAllDocumentsAsync(cancellationToken);
        if (result.EndpointCount == 0)
        {
            return;
        }

        SwaggerTelemetry.SetEndpointCount(result.EndpointCount);

        _logger.LogInformation(
            "Completed swagger refresh for {DocumentCount} documents from {EndpointCount} endpoints ({RefreshedCount} succeeded, {FailedCount} failed)",
            result.DocumentCount,
            result.EndpointCount,
            result.RefreshedCount,
            result.FailedCount);

        stopwatch.Stop();
        SwaggerTelemetry.RefreshDuration.Record(stopwatch.ElapsedMilliseconds);
        SwaggerTelemetry.RefreshCounter.Add(1);
    }
}
