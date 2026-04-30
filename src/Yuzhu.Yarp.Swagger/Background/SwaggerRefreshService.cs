using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// Hosted background service that drives refreshes through a composite of
/// <see cref="ISwaggerRefreshTrigger"/>s. Event-driven; the periodic trigger is just one
/// among several inputs.
/// </summary>
public sealed class SwaggerRefreshService(
    SwaggerDocumentCoordinator coordinator,
    IEnumerable<ISwaggerRefreshTrigger> triggers,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<SwaggerRefreshService> logger) : BackgroundService
{
    private readonly SwaggerDocumentCoordinator _coordinator = coordinator;
    private readonly IReadOnlyList<ISwaggerRefreshTrigger> _triggers = [.. triggers];
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<SwaggerRefreshService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan startupDelay = _options.CurrentValue.StartupDelay;
        if (startupDelay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Swagger refresh service starting; waiting {Delay} for YARP initialization",
                startupDelay);
            try
            {
                await Task.Delay(startupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogInformation(
            "Swagger refresh service started with {TriggerCount} trigger(s)",
            _triggers.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during Swagger refresh");
            }

            try
            {
                await WaitForNextTriggerAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        using Activity? activity = SwaggerTelemetry.ActivitySource.StartActivity("RefreshAllDocuments");

        SwaggerRefreshResult result = await _coordinator.RefreshAllAsync(cancellationToken);

        SwaggerTelemetry.SetEndpointCount(result.EndpointCount);
        stopwatch.Stop();
        SwaggerTelemetry.RefreshDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        SwaggerTelemetry.RefreshCounter.Add(1);

        if (result.EndpointCount == 0)
        {
            _logger.LogWarning(
                "Swagger discovery returned 0 endpoints. Diagnostics: {Diagnostics}",
                FormatDiagnostics(result.Diagnostics));
            return;
        }

        _logger.LogInformation(
            "Swagger refresh: {EndpointCount} endpoints, {DocumentCount} documents, {RefreshedCount} succeeded, {FailedCount} failed in {Duration}ms",
            result.EndpointCount,
            result.DocumentCount,
            result.RefreshedCount,
            result.FailedCount,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private async Task WaitForNextTriggerAsync(CancellationToken stoppingToken)
    {
        if (_triggers.Count == 0)
        {
            // No triggers configured; treat the service as one-shot.
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        IChangeToken token = _triggers.Count == 1
            ? _triggers[0].GetReloadToken()
            : new CompositeChangeToken([.. _triggers.Select(t => t.GetReloadToken())]);

        if (!token.ActiveChangeCallbacks)
        {
            // Periodic trigger doesn't support callbacks - poll instead.
            while (!token.HasChanged)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
            return;
        }

        TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable registration = token.RegisterChangeCallback(_ => tcs.TrySetResult(), state: null);
        using CancellationTokenRegistration cancelRegistration =
            stoppingToken.Register(() => tcs.TrySetResult());

        await tcs.Task;
    }

    private static string FormatDiagnostics(IReadOnlyList<SwaggerEndpointDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "(none)"
            : string.Join(
                "; ",
                diagnostics.Select(d => $"{d.ClusterId}|{d.Stage}|{d.Severity}: {d.Message}"));
}
