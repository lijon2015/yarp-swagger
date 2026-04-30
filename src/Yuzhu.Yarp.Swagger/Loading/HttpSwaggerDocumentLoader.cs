using System.Diagnostics;
using System.Net.Http.Headers;
using Duende.AccessTokenManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Polly.Timeout;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Telemetry;

namespace Yuzhu.Yarp.Swagger.Loading;

/// <summary>
/// HTTP-based loader that fetches a backend Swagger JSON document, validates the size, and
/// parses it through <see cref="OpenApiDocument.LoadAsync(System.IO.Stream, string?, OpenApiReaderSettings?, CancellationToken)"/>.
/// Per-attempt timeouts and retries come from the resilience pipeline configured on the
/// named HTTP client.
/// </summary>
public sealed class HttpSwaggerDocumentLoader(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SwaggerAggregationOptions> options,
    ILogger<HttpSwaggerDocumentLoader> logger,
    IClientCredentialsTokenManager? tokenManager = null) : ISwaggerDocumentLoader
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<SwaggerAggregationOptions> _options = options;
    private readonly ILogger<HttpSwaggerDocumentLoader> _logger = logger;
    private readonly IClientCredentialsTokenManager? _tokenManager = tokenManager;

    public async Task<SwaggerLoadResult> LoadAsync(
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        SwaggerAggregationOptions options = _options.CurrentValue;

        using Activity? activity = SwaggerTelemetry.ActivitySource.StartActivity("LoadSwaggerDocument");
        _ = activity?.SetTag("cluster.id", endpoint.ClusterId);
        _ = activity?.SetTag("document.name", endpoint.DocumentName);
        _ = activity?.SetTag("destination.address", endpoint.BaseAddress.ToString());
        _ = activity?.SetTag("swagger.path", endpoint.SwaggerPath);

        HttpClient httpClient = _httpClientFactory.CreateClient(SwaggerConstants.HttpClientName);

        // Outer budget covers all attempts plus a small buffer; resilience pipeline owns
        // per-attempt timeouts and retries.
        TimeSpan budget = TimeSpan.FromMilliseconds(
            options.LoadTimeout.TotalMilliseconds * (options.MaxRetryAttempts + 1) + 5_000);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(budget);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, endpoint.SwaggerUrl);
            await TryAttachAccessTokenAsync(request, endpoint, cts.Token);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return RecordFailure(
                    endpoint,
                    stopwatch,
                    failureStage: "http",
                    httpStatus: (int)response.StatusCode,
                    error: $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            int maxSize = options.MaxDocumentSizeBytes;
            long? declaredLength = response.Content.Headers.ContentLength;

            if (declaredLength is long declared && declared > maxSize)
            {
                return RecordFailure(
                    endpoint,
                    stopwatch,
                    failureStage: "size",
                    httpStatus: (int)response.StatusCode,
                    error: $"Declared Content-Length {declared} exceeds {maxSize} bytes");
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using MemoryStream memory = new(declaredLength is > 0 and <= int.MaxValue
                ? (int)declaredLength.Value
                : 8192);

            byte[] buffer = new byte[8192];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cts.Token)) > 0)
            {
                total += read;
                if (total > maxSize)
                {
                    return RecordFailure(
                        endpoint,
                        stopwatch,
                        failureStage: "size",
                        httpStatus: (int)response.StatusCode,
                        error: $"Document size exceeds {maxSize} bytes");
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cts.Token);
            }

            memory.Position = 0;

            ReadResult readResult = await OpenApiDocument.LoadAsync(memory, cancellationToken: cts.Token);
            if (readResult.Document is null)
            {
                return RecordFailure(
                    endpoint,
                    stopwatch,
                    failureStage: "parse",
                    httpStatus: (int)response.StatusCode,
                    error: "Failed to parse OpenAPI document");
            }

            stopwatch.Stop();

            SwaggerTelemetry.LoadSuccessCounter.Add(1,
                new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                new KeyValuePair<string, object?>("document.name", endpoint.DocumentName));
            SwaggerTelemetry.LoadDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
                new KeyValuePair<string, object?>("document.name", endpoint.DocumentName));

            _logger.LogInformation(
                "Loaded Swagger document for {ClusterId} ({DocumentName}) from {Url} in {Duration}ms",
                endpoint.ClusterId,
                endpoint.DocumentName,
                endpoint.SwaggerUrl,
                stopwatch.Elapsed.TotalMilliseconds);

            return new SwaggerLoadResult
            {
                Endpoint = endpoint,
                Document = readResult.Document,
                HttpStatusCode = (int)response.StatusCode,
                LoadDuration = stopwatch.Elapsed,
            };
        }
        catch (HttpRequestException ex)
        {
            return RecordFailure(endpoint, stopwatch, failureStage: "http", httpStatus: null, error: ex.Message, ex);
        }
        catch (Exception ex) when (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return RecordFailure(endpoint, stopwatch, failureStage: "timeout", httpStatus: null, error: "Request timed out");
        }
        catch (TimeoutRejectedException)
        {
            return RecordFailure(endpoint, stopwatch, failureStage: "timeout", httpStatus: null, error: "Request timed out");
        }
        catch (Exception ex)
        {
            return RecordFailure(endpoint, stopwatch, failureStage: "unknown", httpStatus: null, error: ex.Message, ex);
        }
    }

    private async Task TryAttachAccessTokenAsync(
        HttpRequestMessage request,
        SwaggerEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(endpoint.AccessTokenClient) || _tokenManager is null)
        {
            return;
        }

        try
        {
            ClientCredentialsToken token = await _tokenManager
                .GetAccessTokenAsync(
                    ClientCredentialsClientName.Parse(endpoint.AccessTokenClient),
                    ct: cancellationToken)
                .GetToken();

            if (!string.IsNullOrEmpty(token.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to acquire access token for cluster {ClusterId} via client {ClientName}",
                endpoint.ClusterId,
                endpoint.AccessTokenClient);
        }
    }

    private SwaggerLoadResult RecordFailure(
        SwaggerEndpoint endpoint,
        Stopwatch stopwatch,
        string failureStage,
        int? httpStatus,
        string error,
        Exception? exception = null)
    {
        stopwatch.Stop();

        if (exception is null)
        {
            _logger.LogWarning(
                "Failed to load Swagger document for {ClusterId} ({DocumentName}) from {Url}: {Stage} {Error}",
                endpoint.ClusterId,
                endpoint.DocumentName,
                endpoint.SwaggerUrl,
                failureStage,
                error);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Failed to load Swagger document for {ClusterId} ({DocumentName}) from {Url}: {Stage}",
                endpoint.ClusterId,
                endpoint.DocumentName,
                endpoint.SwaggerUrl,
                failureStage);
        }

        SwaggerTelemetry.LoadFailureCounter.Add(1,
            new KeyValuePair<string, object?>("cluster.id", endpoint.ClusterId),
            new KeyValuePair<string, object?>("document.name", endpoint.DocumentName),
            new KeyValuePair<string, object?>("failure.stage", failureStage),
            new KeyValuePair<string, object?>("http.status_code", httpStatus));

        return new SwaggerLoadResult
        {
            Endpoint = endpoint,
            HttpStatusCode = httpStatus,
            FailureStage = failureStage,
            ErrorMessage = error,
            LoadDuration = stopwatch.Elapsed,
        };
    }
}
