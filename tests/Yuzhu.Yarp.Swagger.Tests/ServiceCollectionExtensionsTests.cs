using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Model;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Discovery;
using Yuzhu.Yarp.Swagger.Extensions;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSwaggerAggregation_RegistersHybridEndpointProviderAsDefault()
    {
        using var provider = BuildProvider();

        var endpointProvider = provider.GetRequiredService<ISwaggerEndpointProvider>();

        Assert.IsType<HybridSwaggerEndpointProvider>(endpointProvider);
    }

    [Fact]
    public void AddSwaggerAggregation_RegistersAggregatedProviderForBothSyncAndAsyncSwashbuckleContracts()
    {
        using var provider = BuildProvider();

        var aggregated = provider.GetRequiredService<AggregatedSwaggerProvider>();
        var async = provider.GetRequiredService<IAsyncSwaggerProvider>();
        var sync = provider.GetRequiredService<ISwaggerProvider>();

        Assert.Same(aggregated, async);
        Assert.Same(aggregated, sync);
    }

    [Fact]
    public void AddSwaggerAggregation_BindsOptionsFromConfiguration()
    {
        var configured = TimeSpan.FromSeconds(42);
        using var provider = BuildProvider(configOverrides: new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = configured.ToString(),
        });

        var options = provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(configured, options.RefreshInterval);
    }

    /// <summary>
    /// v2.0.0 switched <see cref="SwaggerAggregationBuilder.Configure"/> and the
    /// overload taking <c>Action&lt;SwaggerAggregationOptions&gt;</c> from
    /// <c>Configure</c> to <c>PostConfigure</c>, so user overrides must win over
    /// values loaded by <c>BindConfiguration</c>.
    /// </summary>
    [Fact]
    public void AddSwaggerAggregation_ConfigureOptionsOverload_RunsAsPostConfigureAndOverridesConfiguration()
    {
        var configValue = TimeSpan.FromSeconds(10);
        var overrideValue = TimeSpan.FromMinutes(9);

        var services = CreateBaseServices(new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = configValue.ToString(),
        });

        BuildBuilder(services).AddSwaggerAggregation(
            options => options.RefreshInterval = overrideValue);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(overrideValue, resolved.RefreshInterval);
    }

    [Fact]
    public void AddSwaggerAggregation_BuilderConfigure_RunsAsPostConfigureAndOverridesConfiguration()
    {
        var configValue = TimeSpan.FromSeconds(10);
        var overrideValue = TimeSpan.FromMinutes(9);

        var services = CreateBaseServices(new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = configValue.ToString(),
        });

        BuildBuilder(services).AddSwaggerAggregation(builder =>
            builder.Configure(o => o.RefreshInterval = overrideValue));

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(overrideValue, resolved.RefreshInterval);
    }

    [Fact]
    public void AddSwaggerAggregation_BuilderUseEndpointProvider_ReplacesHybridDefault()
    {
        var services = CreateBaseServices();

        BuildBuilder(services).AddSwaggerAggregation(builder =>
            builder.UseEndpointProvider<ConfigBasedSwaggerEndpointProvider>());

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<ISwaggerEndpointProvider>();

        Assert.IsType<ConfigBasedSwaggerEndpointProvider>(resolved);
    }

    private static ServiceProvider BuildProvider(IDictionary<string, string?>? configOverrides = null)
    {
        var services = CreateBaseServices(configOverrides);
        BuildBuilder(services).AddSwaggerAggregation();
        return services.BuildServiceProvider();
    }

    private static IServiceCollection CreateBaseServices(IDictionary<string, string?>? configOverrides = null)
    {
        var configBuilder = new ConfigurationBuilder();
        if (configOverrides is { Count: > 0 })
        {
            configBuilder.AddInMemoryCollection(configOverrides);
        }

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(configBuilder.Build());
        services.AddSingleton<IProxyStateLookup, StubProxyStateLookup>();
        return services;
    }

    private static IReverseProxyBuilder BuildBuilder(IServiceCollection services) =>
        new FakeReverseProxyBuilder(services);

    private sealed class FakeReverseProxyBuilder(IServiceCollection services) : IReverseProxyBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class StubProxyStateLookup : IProxyStateLookup
    {
        public bool TryGetRoute(string id, [NotNullWhen(true)] out RouteModel? route)
        {
            route = null;
            return false;
        }

        public bool TryGetCluster(string id, [NotNullWhen(true)] out ClusterState? cluster)
        {
            cluster = null;
            return false;
        }

        public IEnumerable<RouteModel> GetRoutes() => [];

        public IEnumerable<ClusterState> GetClusters() => [];
    }
}
