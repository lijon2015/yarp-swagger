using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using System.Net;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Resilience;

/// <summary>
/// Swagger HTTP 客户端弹性扩展方法
/// </summary>
public static class SwaggerHttpClientExtensions
{
    /// <summary>
    /// 添加 Swagger 弹性处理管道（重试 + 每次尝试超时 + 熔断）。
    /// 配置在请求时按当前 <see cref="SwaggerAggregationOptions"/> 解析。
    /// </summary>
    public static IHttpClientBuilder AddSwaggerResilienceHandler(this IHttpClientBuilder builder)
    {
        _ = builder.AddResilienceHandler("swagger-pipeline", static (configure, context) =>
        {
            SwaggerAggregationOptions options = context.ServiceProvider
                .GetRequiredService<IOptionsMonitor<SwaggerAggregationOptions>>()
                .CurrentValue;

            _ = configure
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException or TimeoutException ||
                        args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError)
                })
                .AddTimeout(options.LoadTimeout)
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException or TimeoutException)
                });
        });

        return builder;
    }
}
