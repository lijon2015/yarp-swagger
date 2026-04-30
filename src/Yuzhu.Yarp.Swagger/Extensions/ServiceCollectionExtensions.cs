using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Yarp.ReverseProxy.Configuration;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Background;
using Yuzhu.Yarp.Swagger.Background.Triggers;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Discovery.AddressResolution;
using Yuzhu.Yarp.Swagger.Discovery.Sources;
using Yuzhu.Yarp.Swagger.Loading;
using Yuzhu.Yarp.Swagger.Merging;
using Yuzhu.Yarp.Swagger.Resilience;
using Yuzhu.Yarp.Swagger.Storage;
using Yuzhu.Yarp.Swagger.Telemetry;
using Yuzhu.Yarp.Swagger.Transforming;

namespace Yuzhu.Yarp.Swagger.Extensions;

/// <summary>
/// Service registration entry point. Registers the discovery pipeline, loader, merger,
/// store, telemetry, refresh service, and Swashbuckle adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Add Swagger aggregation services to a YARP reverse proxy builder.</summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        IServiceCollection services = builder.Services;

        _ = services.AddOptions<SwaggerAggregationOptions>()
            .BindConfiguration(SwaggerAggregationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        _ = services.AddOptions<SwaggerAggregationDocumentEndpointOptions>();

        _ = services.AddHttpClient(SwaggerConstants.HttpClientName, static client =>
        {
            // Resilience pipeline + outer aggregation timeout own cancellation.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(static sp =>
        {
            SwaggerAggregationOptions options = sp
                .GetRequiredService<IOptionsMonitor<SwaggerAggregationOptions>>()
                .CurrentValue;

            return new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = options.MaxParallelism,
            };
        })
        .AddSwaggerResilienceHandler();

        // Discovery pipeline. Sources run in registration order; runtime first because the
        // YARP runtime state reflects dynamic destinations and configured destinations are
        // a fallback only.
        _ = services.AddSingleton<ISwaggerEndpointSource, YarpRuntimeSwaggerEndpointSource>();
        _ = services.AddSingleton<ISwaggerEndpointSource, YarpConfigurationSwaggerEndpointSource>();

        _ = services.AddSingleton<ISwaggerEndpointAddressResolver, YarpRuntimeDestinationAddressResolver>();
        _ = services.AddSingleton<ISwaggerEndpointAddressResolver, YarpConfiguredDestinationAddressResolver>();

        services.TryAddSingleton<ISwaggerEndpointDiscoveryService, SwaggerEndpointDiscoveryService>();

        // Pipeline.
        services.TryAddSingleton<ISwaggerDocumentLoader, HttpSwaggerDocumentLoader>();
        services.TryAddSingleton<ISwaggerDocumentMerger, DefaultSwaggerDocumentMerger>();
        services.TryAddSingleton<ISwaggerAggregator, SwaggerAggregator>();
        services.TryAddSingleton<IAggregatedDocumentStore, InMemoryAggregatedDocumentStore>();
        services.TryAddSingleton<SwaggerDocumentCoordinator>();

        // Default transformers - additional transformers can be appended via the builder.
        _ = services.AddSingleton<ISwaggerDocumentTransformer, PathPrefixTransformer>();
        _ = services.AddSingleton<ISwaggerDocumentTransformer, PathFilterTransformer>();

        // Refresh triggers. Runtime YARP config change is the primary signal; options change
        // and periodic timer keep refreshes flowing in environments without dynamic config.
        // YARP trigger is registered conditionally - if the host hasn't called
        // AddReverseProxy() (and so has no IProxyConfigProvider), a no-op trigger is used.
        _ = services.AddSingleton<ISwaggerRefreshTrigger, OptionsChangeRefreshTrigger>();
        _ = services.AddSingleton<ISwaggerRefreshTrigger, PeriodicRefreshTrigger>();
        _ = services.AddSingleton<ISwaggerRefreshTrigger>(static sp =>
        {
            IProxyConfigProvider? provider = sp.GetService<IProxyConfigProvider>();
            return provider is null
                ? new NullRefreshTrigger()
                : new YarpConfigChangeRefreshTrigger(provider);
        });

        _ = services.AddHostedService<SwaggerRefreshService>();

        // Swashbuckle adapter is registered as a concrete service for callers that
        // explicitly need it, but it is not wired to ISwaggerProvider by default.
        // Visual Studio and Swashbuckle tooling probe the default "v1" document name;
        // routing those probes through the aggregation provider turns an empty design-time
        // document request into an UnknownSwaggerDocument exception.
        services.TryAddSingleton<AggregatedSwaggerProvider>();

        _ = services.AddSwaggerTelemetry();

        if (configure is not null)
        {
            SwaggerAggregationBuilder aggregationBuilder = new(services);
            configure(aggregationBuilder);
        }

        return builder;
    }

    /// <summary>Add Swagger aggregation with an inline options override.</summary>
    public static IReverseProxyBuilder AddSwaggerAggregation(
        this IReverseProxyBuilder builder,
        Action<SwaggerAggregationOptions> configureOptions,
        Action<SwaggerAggregationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        _ = builder.AddSwaggerAggregation(configure);
        _ = builder.Services.PostConfigure(configureOptions);
        return builder;
    }

    /// <summary>
    /// Sentinel trigger used when no <see cref="IProxyConfigProvider"/> is registered. Its
    /// token never fires, so it contributes nothing to the composite trigger.
    /// </summary>
    private sealed class NullRefreshTrigger : ISwaggerRefreshTrigger
    {
        private static readonly Microsoft.Extensions.Primitives.IChangeToken NeverChanges =
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => NeverChanges;
    }
}
