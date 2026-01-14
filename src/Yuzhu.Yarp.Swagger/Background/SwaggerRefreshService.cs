using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Background;

/// <summary>
/// 后台刷新服务 - 定期刷新 Swagger 文档
/// </summary>
public sealed class SwaggerRefreshService : BackgroundService
{
    private readonly ISwaggerAggregator _aggregator;
    private readonly ISwaggerEndpointProvider _endpointProvider;
    private readonly IAggregatedDocumentStore _documentStore;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options;
    private readonly ILogger<SwaggerRefreshService> _logger;

    private CancellationTokenSource? _configChangeCts;
    private readonly object _ctsLock = new();

    public SwaggerRefreshService(
        ISwaggerAggregator aggregator,
        ISwaggerEndpointProvider endpointProvider,
        IAggregatedDocumentStore documentStore,
        IOptionsMonitor<SwaggerAggregationOptions> options,
        ILogger<SwaggerRefreshService> logger)
    {
        _aggregator = aggregator;
        _endpointProvider = endpointProvider;
        _documentStore = documentStore;
        _options = options;
        _logger = logger;

        // 订阅配置变更
        _options.OnChange(_ =>
        {
            _logger.LogInformation("Configuration changed, triggering refresh");
            TriggerRefresh();
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动时等待 YARP 初始化完成
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

            // 等待下一次刷新，或被配置变更中断
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
                    stoppingToken, configCts.Token);

                await Task.Delay(_options.CurrentValue.RefreshInterval, linkedCts.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                // 配置变更触发的取消，继续循环立即刷新
                _logger.LogDebug("Refresh triggered by configuration change");
            }
        }
    }

    private async Task RefreshAllDocumentsAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        using var activity = SwaggerTelemetry.ActivitySource.StartActivity("RefreshAllDocuments");

        var endpoints = _endpointProvider.GetEndpoints();

        if (endpoints.Count == 0)
        {
            _logger.LogDebug("No swagger endpoints discovered");
            return;
        }

        SwaggerTelemetry.SetEndpointCount(endpoints.Count);

        _logger.LogInformation(
            "Starting swagger refresh for {EndpointCount} endpoints",
            endpoints.Count);

        // 按文档名称分组
        var documentNames = endpoints
            .Select(e => e.DocumentName ?? e.ClusterId)
            .Distinct();

        foreach (var docName in documentNames)
        {
            try
            {
                var docEndpoints = endpoints
                    .Where(e => (e.DocumentName ?? e.ClusterId) == docName)
                    .ToList();

                var context = new AggregationContext
                {
                    DocumentName = docName,
                    Endpoints = docEndpoints
                };

                var document = await _aggregator.AggregateAsync(context, cancellationToken);

                // 存储聚合结果
                await _documentStore.SetAsync(docName, document, cancellationToken);

                _logger.LogInformation(
                    "Refreshed swagger document '{DocumentName}' with {PathCount} paths in {Duration}ms",
                    docName, document.Paths.Count, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to refresh swagger document '{DocumentName}'",
                    docName);
            }
        }

        sw.Stop();
        SwaggerTelemetry.RefreshDuration.Record(sw.ElapsedMilliseconds);
        SwaggerTelemetry.RefreshCounter.Add(1);
    }

    /// <summary>
    /// 手动触发刷新
    /// </summary>
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
                // 已释放，忽略
            }
        }
    }

    public override void Dispose()
    {
        lock (_ctsLock)
        {
            _configChangeCts?.Dispose();
            _configChangeCts = null;
        }
        base.Dispose();
    }
}
