using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Resilience;

/// <summary>
/// Swagger HTTP 客户端弹性扩展方法
/// </summary>
public static class SwaggerHttpClientExtensions
{
    /// <summary>
    /// 添加 Swagger 弹性处理管道
    /// </summary>
    public static void AddSwaggerResilienceHandler(
        this IHttpClientBuilder builder,
        SwaggerAggregationOptions options)
    {
        builder.AddResilienceHandler("swagger-pipeline", configure =>
        {
            configure
                // 重试策略
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        args.Outcome.Exception is TimeoutException ||
                        args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError)
                })
                // 超时策略
                .AddTimeout(options.LoadTimeout)
                // 熔断策略
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException ||
                        args.Outcome.Exception is TimeoutException)
                });
        });
    }
}
