using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Background;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Loading;
using Yuzhu.Yarp.Swagger.Merging;
using Yuzhu.Yarp.Swagger.Resilience;
using Yuzhu.Yarp.Swagger.Storage;
using Yuzhu.Yarp.Swagger.Telemetry;
using Yuzhu.Yarp.Swagger.Transforming;

namespace Yuzhu.Yarp.Swagger.Extensions;

/// <summary>
/// Extension methods for registering Swagger aggregation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Swagger aggregation services to the reverse proxy builder.
    /// </summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        var services = builder.Services;

        services.AddOptions<SwaggerAggregationOptions>()
            .BindConfiguration(SwaggerAggregationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Keep the HTTP client timeout disabled and let the resilience pipeline
        // plus the outer aggregation timeout own cancellation behavior.
        services.AddHttpClient(SwaggerConstants.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<SwaggerAggregationOptions>>().CurrentValue;

            return new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = options.MaxParallelism
            };
        })
        .AddSwaggerResilienceHandler();

        services.TryAddSingleton<ConfigBasedSwaggerEndpointProvider>();
        services.TryAddSingleton<YarpStateSwaggerEndpointProvider>();
        services.TryAddSingleton<ISwaggerEndpointProvider, HybridSwaggerEndpointProvider>();
        services.TryAddSingleton<ISwaggerDocumentLoader, HttpSwaggerDocumentLoader>();
        services.TryAddSingleton<ISwaggerDocumentMerger, DefaultSwaggerDocumentMerger>();
        services.TryAddSingleton<ISwaggerAggregator, SwaggerAggregator>();
        services.TryAddSingleton<IAggregatedDocumentStore, InMemoryAggregatedDocumentStore>();
        services.TryAddSingleton<SwaggerDocumentCoordinator>();

        services.AddSingleton<ISwaggerDocumentTransformer, PathPrefixTransformer>();
        services.AddSingleton<ISwaggerDocumentTransformer, PathFilterTransformer>();

        services.AddHostedService<SwaggerRefreshService>();

        // Swashbuckle's official contract still requires ISwaggerProvider.
        // Register both interfaces so the middleware can use async when available
        // without breaking the required DI contract.
        services.TryAddSingleton<AggregatedSwaggerProvider>();
        services.AddSingleton<IAsyncSwaggerProvider>(sp => sp.GetRequiredService<AggregatedSwaggerProvider>());
        services.AddSingleton<ISwaggerProvider>(sp => sp.GetRequiredService<AggregatedSwaggerProvider>());

        services.AddSwaggerTelemetry();

        if (configure != null)
        {
            var aggregationBuilder = new SwaggerAggregationBuilder(services);
            configure(aggregationBuilder);
            aggregationBuilder.Build();
        }

        return builder;
    }

    /// <summary>
    /// Adds Swagger aggregation services with option overrides.
    /// </summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationOptions> configureOptions,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        var result = builder.AddSwaggerAggregation(configure);
        builder.Services.PostConfigure(configureOptions);
        return result;
    }
}
