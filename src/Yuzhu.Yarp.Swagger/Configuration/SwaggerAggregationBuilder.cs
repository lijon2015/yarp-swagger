using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Fluent builder used inside <c>AddSwaggerAggregation(builder => ...)</c> to register
/// transformers, replace pipeline components, and add custom sources / resolvers / triggers.
/// </summary>
public sealed class SwaggerAggregationBuilder
{
    /// <summary>The underlying service collection.</summary>
    public IServiceCollection Services { get; }

    internal SwaggerAggregationBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>Configure <see cref="SwaggerAggregationOptions"/> after configuration binding.</summary>
    public SwaggerAggregationBuilder Configure(Action<SwaggerAggregationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _ = Services.PostConfigure(configure);
        return this;
    }

    /// <summary>
    /// Configure <see cref="SwaggerAggregationDocumentEndpointOptions"/> (route prefix and
    /// unavailable status used by <c>UseSwaggerAggregationDocuments</c>).
    /// </summary>
    public SwaggerAggregationBuilder ConfigureDocumentEndpoint(
        Action<SwaggerAggregationDocumentEndpointOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _ = Services.PostConfigure(configure);
        return this;
    }

    /// <summary>Append a document transformer to the pipeline. Multiple transformers are allowed.</summary>
    public SwaggerAggregationBuilder AddTransformer<TTransformer>()
        where TTransformer : class, ISwaggerDocumentTransformer
    {
        _ = Services.AddSingleton<ISwaggerDocumentTransformer, TTransformer>();
        return this;
    }

    /// <summary>Append a custom endpoint source. Order of registration becomes order of dedup.</summary>
    public SwaggerAggregationBuilder AddSource<TSource>()
        where TSource : class, ISwaggerEndpointSource
    {
        _ = Services.AddSingleton<ISwaggerEndpointSource, TSource>();
        return this;
    }

    /// <summary>
    /// Append a custom address resolver. Resolvers run in registration order; the first to
    /// return a resolved address wins. The two built-in resolvers run before any added here
    /// unless they are removed via <see cref="ClearDefaultResolvers"/>.
    /// </summary>
    public SwaggerAggregationBuilder AddAddressResolver<TResolver>()
        where TResolver : class, ISwaggerEndpointAddressResolver
    {
        _ = Services.AddSingleton<ISwaggerEndpointAddressResolver, TResolver>();
        return this;
    }

    /// <summary>Insert a custom address resolver at the front of the chain.</summary>
    public SwaggerAggregationBuilder InsertAddressResolver<TResolver>()
        where TResolver : class, ISwaggerEndpointAddressResolver
    {
        Services.Insert(
            FindFirstResolverIndex(Services),
            ServiceDescriptor.Singleton<ISwaggerEndpointAddressResolver, TResolver>());
        return this;
    }

    /// <summary>Append a refresh trigger. Multiple triggers are composed.</summary>
    public SwaggerAggregationBuilder AddRefreshTrigger<TTrigger>()
        where TTrigger : class, ISwaggerRefreshTrigger
    {
        _ = Services.AddSingleton<ISwaggerRefreshTrigger, TTrigger>();
        return this;
    }

    /// <summary>Replace the default in-memory document store.</summary>
    public SwaggerAggregationBuilder UseDocumentStore<TStore>()
        where TStore : class, IAggregatedDocumentStore
    {
        _ = Services.RemoveAll<IAggregatedDocumentStore>();
        _ = Services.AddSingleton<IAggregatedDocumentStore, TStore>();
        return this;
    }

    /// <summary>Replace the default HTTP loader.</summary>
    public SwaggerAggregationBuilder UseDocumentLoader<TLoader>()
        where TLoader : class, ISwaggerDocumentLoader
    {
        _ = Services.RemoveAll<ISwaggerDocumentLoader>();
        _ = Services.AddSingleton<ISwaggerDocumentLoader, TLoader>();
        return this;
    }

    /// <summary>Replace the default merger.</summary>
    public SwaggerAggregationBuilder UseDocumentMerger<TMerger>()
        where TMerger : class, ISwaggerDocumentMerger
    {
        _ = Services.RemoveAll<ISwaggerDocumentMerger>();
        _ = Services.AddSingleton<ISwaggerDocumentMerger, TMerger>();
        return this;
    }

    /// <summary>
    /// Remove all currently registered <see cref="ISwaggerEndpointAddressResolver"/>s. Use
    /// when the project wants to bypass the built-in YARP resolvers entirely.
    /// </summary>
    public SwaggerAggregationBuilder ClearDefaultResolvers()
    {
        _ = Services.RemoveAll<ISwaggerEndpointAddressResolver>();
        return this;
    }

    /// <summary>Remove all currently registered <see cref="ISwaggerEndpointSource"/>s.</summary>
    public SwaggerAggregationBuilder ClearDefaultSources()
    {
        _ = Services.RemoveAll<ISwaggerEndpointSource>();
        return this;
    }

    private static int FindFirstResolverIndex(IServiceCollection services)
    {
        for (int index = 0; index < services.Count; index++)
        {
            if (services[index].ServiceType == typeof(ISwaggerEndpointAddressResolver))
            {
                return index;
            }
        }

        return services.Count;
    }
}
