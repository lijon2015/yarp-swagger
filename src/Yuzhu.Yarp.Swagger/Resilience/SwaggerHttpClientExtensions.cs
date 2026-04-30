using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Yuzhu.Yarp.Swagger.Configuration;

namespace Yuzhu.Yarp.Swagger.Resilience;

/// <summary>
/// Adds the resilience pipeline used by the document loader: retry on transient HTTP
/// failure, per-attempt timeout, and a circuit breaker. Values are read from the current
/// <see cref="SwaggerAggregationOptions"/> at request time.
/// </summary>
public static class SwaggerHttpClientExtensions
{
    /// <summary>Add the Swagger loader resilience handler to the given HTTP client builder.</summary>
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
                        args.Outcome.Result?.StatusCode >= HttpStatusCode.InternalServerError),
                })
                .AddTimeout(options.LoadTimeout)
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = static args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException or TimeoutException),
                });
        });

        return builder;
    }
}
