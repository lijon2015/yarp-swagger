using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;
using Yuzhu.Yarp.Swagger.Abstractions;
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Configuration;
using Yuzhu.Yarp.Swagger.Coordination;
using Yuzhu.Yarp.Swagger.Storage;

namespace Yuzhu.Yarp.Swagger.Tests;

public sealed class SwaggerUIConfigurationTests
{
    [Fact]
    public void ConfigureAggregatedEndpoints_WhenDiscoveryEmpty_DefaultsToEmptyUrls()
    {
        SwaggerUIOptions options = new();
        IServiceProvider sp = BuildProvider(SwaggerEndpointDiscoveryResult.Empty);

        _ = options.ConfigureAggregatedEndpoints(sp);

        Assert.Empty(options.ConfigObject.Urls);
    }

    [Fact]
    public void ConfigureAggregatedEndpoints_RegistersOnlyDiscoveredDocuments()
    {
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing-cluster", "billing");

        SwaggerUIOptions options = new();
        IServiceProvider sp = BuildProvider(new SwaggerEndpointDiscoveryResult([orders, billing], []));

        _ = options.ConfigureAggregatedEndpoints(sp);

        Assert.NotNull(options.ConfigObject.Urls);
        UrlDescriptor[] urls = [.. options.ConfigObject.Urls];
        Assert.Equal(2, urls.Length);
        Assert.Contains(urls, u => u.Name == "orders" && u.Url == "/swagger/orders/swagger.json");
        Assert.Contains(urls, u => u.Name == "billing" && u.Url == "/swagger/billing/swagger.json");
    }

    [Fact]
    public void ConfigureAggregatedEndpoints_DiagnosticBehavior_RegistersDiagnosticEndpointOnly()
    {
        SwaggerUIOptions options = new();
        IServiceProvider sp = BuildProvider(SwaggerEndpointDiscoveryResult.Empty);

        _ = options.ConfigureAggregatedEndpoints(
            sp,
            ui => ui.EmptyBehavior = EmptySwaggerEndpointBehavior.DiagnosticEndpoint);

        Assert.NotNull(options.ConfigObject.Urls);
        UrlDescriptor descriptor = Assert.Single(options.ConfigObject.Urls);
        Assert.Equal("Swagger Aggregation Diagnostics", descriptor.Name);
        Assert.DoesNotContain("/v1/", descriptor.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureAggregatedEndpoints_AppliesPrimaryDocumentName()
    {
        SwaggerEndpoint orders = TestDoubles.CreateEndpoint("orders-cluster", "orders");
        SwaggerEndpoint billing = TestDoubles.CreateEndpoint("billing-cluster", "billing");

        SwaggerUIOptions options = new();
        IServiceProvider sp = BuildProvider(new SwaggerEndpointDiscoveryResult([orders, billing], []));

        _ = options.ConfigureAggregatedEndpoints(
            sp,
            ui => ui.PrimaryDocumentName = "billing");

        Assert.True(options.ConfigObject.AdditionalItems.ContainsKey("urls.primaryName"));
        Assert.Equal("billing", options.ConfigObject.AdditionalItems["urls.primaryName"]);
    }

    private static IServiceProvider BuildProvider(SwaggerEndpointDiscoveryResult discovery)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<ISwaggerEndpointDiscoveryService>(new StubDiscoveryService(discovery));
        _ = services.AddSingleton<IAggregatedDocumentStore, InMemoryAggregatedDocumentStore>();
        _ = services.AddSingleton<ISwaggerAggregator>(
            new StubAggregator(_ => TestDoubles.CreateDocument("ignored")));
        _ = services.AddSingleton<IOptionsMonitor<SwaggerAggregationOptions>>(TestDoubles.OptionsMonitor());
        _ = services.AddSingleton<SwaggerDocumentCoordinator>();
        _ = services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        _ = services.AddLogging();
        return services.BuildServiceProvider();
    }
}
