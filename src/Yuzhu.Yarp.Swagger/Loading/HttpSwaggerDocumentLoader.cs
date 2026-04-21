using Duende.AccessTokenManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Polly.Timeout;
using System.Diagnostics;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Loading;

/// <summary>
/// 基于 HTTP 的 Swagger 文档加载器
/// </summary>
public sealed class HttpSwaggerDocumentLoader(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<HttpSwaggerDocumentLoader> logger,
    IClientCredentialsTokenManager? tokenManager = null) : ISwaggerDocumentLoader
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IClientCredentialsTokenManager? _tokenManager = tokenManager;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<HttpSwaggerDocumentLoader> _logger = logger;

    public async Task<SwaggerLoadResult> LoadAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        // 使用具名的 HTTP 客户端
        HttpClient httpClient = _httpClientFactory.CreateClient(SwaggerConstants.HttpClientName);

        using Activity? activity = SwaggerTelemetry.ActivitySource.StartActivity("LoadSwaggerDocument");
        _ = (activity?.SetTag("cluster.id", endpoint.ClusterId));
        _ = (activity?.SetTag("swagger.url", endpoint.SwaggerUrl.ToString()));

        SwaggerAggregationOptions options = _options.CurrentValue;

        // 整体预算 = 单次 LoadTimeout × (重试次数 + 1) + 缓冲，确保弹性管道的重试有时间窗口完成。
        // 弹性管道负责每次尝试的超时和重试；本 CTS 是兜底，避免极端情况下无界等待。
        TimeSpan overallBudget = TimeSpan.FromMilliseconds(
            options.LoadTimeout.TotalMilliseconds * (options.MaxRetryAttempts + 1) + 5000);

        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(overallBudget);

            // 创建请求
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint.SwaggerUrl);

            // 如果配置了 AccessToken 客户端，获取并添加 token
            if (!string.IsNullOrEmpty(endpoint.AccessTokenClient) && _tokenManager != null)
            {
                try
                {
                    ClientCredentialsToken tokenResult = await _tokenManager
                        .GetAccessTokenAsync(ClientCredentialsClientName.Parse(endpoint.AccessTokenClient), ct: cts.Token)
                        .GetToken();

                    if (!string.IsNullOrEmpty(tokenResult.AccessToken))
                    {
                        request.Headers.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                        _logger.LogDebug(
                            "Added access token for {ClusterId} using client {ClientName}",
                            endpoint.ClusterId, endpoint.AccessTokenClient);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to get access token for {ClusterId} using client {ClientName}",
                        endpoint.ClusterId, endpoint.AccessTokenClient);
                }
            }

            using HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);
            _ = response.EnsureSuccessStatusCode();

            int maxSize = options.MaxDocumentSizeBytes;
            long? declaredLength = response.Content.Headers.ContentLength;

            // 优先读取 Content-Length 头进行预校验，避免下载超大文档浪费带宽和内存。
            if (declaredLength.HasValue && declaredLength.Value > maxSize)
            {
                _logger.LogWarning(
                    "Declared Content-Length exceeds limit for {ClusterId}: {Size} > {MaxSize} bytes",
                    endpoint.ClusterId, declaredLength.Value, maxSize);

                SwaggerTelemetry.LoadFailureCounter.Add(1,
                    new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                    new KeyValuePair<string, object?>("error.type", "size_exceeded"));

                sw.Stop();
                return new SwaggerLoadResult
                {
                    Endpoint = endpoint,
                    ErrorMessage = $"Content-Length {declaredLength.Value} exceeds maximum size of {maxSize} bytes",
                    LoadDuration = sw.Elapsed
                };
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);

            // 使用内存流以支持同步读取（带大小限制）
            using MemoryStream memoryStream = new MemoryStream(
                capacity: declaredLength is > 0 and <= int.MaxValue ? (int)declaredLength.Value : 8192);

            byte[] buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, cts.Token)) > 0)
            {
                totalBytesRead += bytesRead;
                if (totalBytesRead > maxSize)
                {
                    _logger.LogWarning(
                        "Document size exceeds limit for {ClusterId}: {Size} > {MaxSize} bytes",
                        endpoint.ClusterId, totalBytesRead, maxSize);

                    SwaggerTelemetry.LoadFailureCounter.Add(1,
                        new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                        new KeyValuePair<string, object?>("error.type", "size_exceeded"));

                    return new SwaggerLoadResult
                    {
                        Endpoint = endpoint,
                        ErrorMessage = $"Document exceeds maximum size of {maxSize} bytes",
                        LoadDuration = sw.Elapsed
                    };
                }
                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
            }

            memoryStream.Position = 0;

            ReadResult readResult = await OpenApiDocument.LoadAsync(memoryStream, cancellationToken: cts.Token);
            OpenApiDocument? document = readResult.Document;

            sw.Stop();

            if (document != null)
            {
                SwaggerTelemetry.LoadSuccessCounter.Add(1,
                    new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId));
                SwaggerTelemetry.LoadDuration.Record(sw.ElapsedMilliseconds,
                    new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId));

                _logger.LogInformation(
                    "Loaded Swagger document for {ClusterId} from {Url} in {Duration}ms",
                    endpoint.ClusterId, endpoint.SwaggerUrl, sw.ElapsedMilliseconds);

                return new SwaggerLoadResult
                {
                    Endpoint = endpoint,
                    Document = document,
                    LoadDuration = sw.Elapsed
                };
            }
            else
            {
                string errorMessage = "Failed to parse OpenAPI document";
                _logger.LogWarning(
                    "Failed to parse Swagger document for {ClusterId} from {Url}",
                    endpoint.ClusterId, endpoint.SwaggerUrl);

                SwaggerTelemetry.LoadFailureCounter.Add(1,
                    new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                    new KeyValuePair<string, object?>("error.type", "parse_error"));

                return new SwaggerLoadResult
                {
                    Endpoint = endpoint,
                    ErrorMessage = errorMessage,
                    LoadDuration = sw.Elapsed
                };
            }
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex,
                "HTTP error loading Swagger for {ClusterId} from {Url}",
                endpoint.ClusterId, endpoint.SwaggerUrl);

            SwaggerTelemetry.LoadFailureCounter.Add(1,
                new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                new KeyValuePair<string, object?>("error.type", "http_error"));

            return new SwaggerLoadResult
            {
                Endpoint = endpoint,
                ErrorMessage = ex.Message,
                LoadDuration = sw.Elapsed
            };
        }
        catch (Exception ex) when (
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested ||
            ex is TimeoutRejectedException)
        {
            sw.Stop();
            _logger.LogWarning(
                "Timeout loading Swagger for {ClusterId} from {Url}",
                endpoint.ClusterId, endpoint.SwaggerUrl);

            SwaggerTelemetry.LoadFailureCounter.Add(1,
                new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                new KeyValuePair<string, object?>("error.type", "timeout"));

            return new SwaggerLoadResult
            {
                Endpoint = endpoint,
                ErrorMessage = "Request timed out",
                LoadDuration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Unexpected error loading Swagger for {ClusterId} from {Url}",
                endpoint.ClusterId, endpoint.SwaggerUrl);

            SwaggerTelemetry.LoadFailureCounter.Add(1,
                new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                new KeyValuePair<string, object?>("error.type", "unknown"));

            return new SwaggerLoadResult
            {
                Endpoint = endpoint,
                ErrorMessage = ex.Message,
                LoadDuration = sw.Elapsed
            };
        }
    }
}
