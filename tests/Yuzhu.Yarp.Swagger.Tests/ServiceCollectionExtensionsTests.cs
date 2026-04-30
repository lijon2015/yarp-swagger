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
using Yuzhu.Yarp.Swagger.Discovery.AddressResolution;
using Yuzhu.Yarp.Swagger.Discovery.Sources;
using Yuzhu.Yarp.Swagger.Extensions;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSwaggerAggregation_RegistersDiscoveryServiceAndDefaultPipeline()
    {
        using ServiceProvider provider = BuildProvider();

        ISwaggerEndpointDiscoveryService discovery =
            provider.GetRequiredService<ISwaggerEndpointDiscoveryService>();

        _ = Assert.IsType<SwaggerEndpointDiscoveryService>(discovery);
    }

    [Fact]
    public void AddSwaggerAggregation_RegistersBothDefaultSourcesInOrder()
    {
        using ServiceProvider provider = BuildProvider();

        ISwaggerEndpointSource[] sources =
            [.. provider.GetServices<ISwaggerEndpointSource>()];

        Assert.Equal(2, sources.Length);
        _ = Assert.IsType<YarpRuntimeSwaggerEndpointSource>(sources[0]);
        _ = Assert.IsType<YarpConfigurationSwaggerEndpointSource>(sources[1]);
    }

    [Fact]
    public void AddSwaggerAggregation_RegistersBothDefaultResolversInOrder()
    {
        using ServiceProvider provider = BuildProvider();

        ISwaggerEndpointAddressResolver[] resolvers =
            [.. provider.GetServices<ISwaggerEndpointAddressResolver>()];

        Assert.Equal(2, resolvers.Length);
        _ = Assert.IsType<YarpRuntimeDestinationAddressResolver>(resolvers[0]);
        _ = Assert.IsType<YarpConfiguredDestinationAddressResolver>(resolvers[1]);
    }

    [Fact]
    public void AddSwaggerAggregation_DoesNotOverrideGlobalSwashbuckleContracts()
    {
        using ServiceProvider provider = BuildProvider();

        AggregatedSwaggerProvider aggregated = provider.GetRequiredService<AggregatedSwaggerProvider>();
        IAsyncSwaggerProvider? async = provider.GetService<IAsyncSwaggerProvider>();
        ISwaggerProvider? sync = provider.GetService<ISwaggerProvider>();

        Assert.NotNull(aggregated);
        Assert.Null(async);
        Assert.Null(sync);
    }

    [Fact]
    public void AddSwaggerAggregation_BindsOptionsFromConfiguration()
    {
        TimeSpan configured = TimeSpan.FromSeconds(42);
        using ServiceProvider provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = configured.ToString(),
        });

        SwaggerAggregationOptions options =
            provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(configured, options.RefreshInterval);
    }

    [Fact]
    public void AddSwaggerAggregation_OptionsActionOverridesConfiguration()
    {
        TimeSpan fromConfig = TimeSpan.FromSeconds(10);
        TimeSpan overrideValue = TimeSpan.FromMinutes(9);

        IServiceCollection services = BaseServices(new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = fromConfig.ToString(),
        });

        _ = new FakeReverseProxyBuilder(services).AddSwaggerAggregation(
            o => o.RefreshInterval = overrideValue);

        using ServiceProvider provider = services.BuildServiceProvider();
        SwaggerAggregationOptions resolved =
            provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(overrideValue, resolved.RefreshInterval);
    }

    [Fact]
    public void AddSwaggerAggregation_BuilderConfigure_OverridesConfiguration()
    {
        TimeSpan fromConfig = TimeSpan.FromSeconds(10);
        TimeSpan overrideValue = TimeSpan.FromMinutes(9);

        IServiceCollection services = BaseServices(new Dictionary<string, string?>
        {
            [$"{SwaggerAggregationOptions.SectionName}:RefreshInterval"] = fromConfig.ToString(),
        });

        _ = new FakeReverseProxyBuilder(services).AddSwaggerAggregation(builder =>
            builder.Configure(o => o.RefreshInterval = overrideValue));

        using ServiceProvider provider = services.BuildServiceProvider();
        SwaggerAggregationOptions resolved =
            provider.GetRequiredService<IOptions<SwaggerAggregationOptions>>().Value;

        Assert.Equal(overrideValue, resolved.RefreshInterval);
    }

    [Fact]
    public void AddSwaggerAggregation_BuilderAddSource_AppendsCustomSource()
    {
        IServiceCollection services = BaseServices();

        _ = new FakeReverseProxyBuilder(services).AddSwaggerAggregation(
            builder => builder.AddSource<NoopEndpointSource>());

        using ServiceProvider provider = services.BuildServiceProvider();
        ISwaggerEndpointSource[] sources = [.. provider.GetServices<ISwaggerEndpointSource>()];

        Assert.Equal(3, sources.Length);
        _ = Assert.IsType<NoopEndpointSource>(sources[2]);
    }

    private static ServiceProvider BuildProvider(IDictionary<string, string?>? configOverrides = null)
    {
        IServiceCollection services = BaseServices(configOverrides);
        _ = new FakeReverseProxyBuilder(services).AddSwaggerAggregation();
        return services.BuildServiceProvider();
    }

    private static IServiceCollection BaseServices(IDictionary<string, string?>? configOverrides = null)
    {
        ConfigurationBuilder configBuilder = new();
        if (configOverrides is { Count: > 0 })
        {
            _ = configBuilder.AddInMemoryCollection(configOverrides);
        }

        ServiceCollection services = new();
        _ = services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        _ = services.AddSingleton<IConfiguration>(configBuilder.Build());
        _ = services.AddSingleton<IProxyStateLookup, StubProxyStateLookup>();
        return services;
    }

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

    private sealed class NoopEndpointSource : ISwaggerEndpointSource
    {
        public ValueTask<IReadOnlyList<SwaggerClusterCandidate>> GetCandidatesAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<SwaggerClusterCandidate>>([]);
    }
}
