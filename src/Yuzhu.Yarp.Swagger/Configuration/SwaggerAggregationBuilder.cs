using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yuzhu.Yarp.Swagger.Abstractions;

namespace Yuzhu.Yarp.Swagger.Configuration;

/// <summary>
/// Swagger 聚合功能构建器
/// </summary>
public sealed class SwaggerAggregationBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _transformerTypes = [];

    internal SwaggerAggregationBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// 添加文档转换器
    /// </summary>
    public SwaggerAggregationBuilder AddTransformer<TTransformer>()
        where TTransformer : class, ISwaggerDocumentTransformer
    {
        _transformerTypes.Add(typeof(TTransformer));
        return this;
    }

    /// <summary>
    /// 自定义端点提供者
    /// </summary>
    public SwaggerAggregationBuilder UseEndpointProvider<TProvider>()
        where TProvider : class, ISwaggerEndpointProvider
    {
        _services.RemoveAll<ISwaggerEndpointProvider>();
        _services.AddSingleton<ISwaggerEndpointProvider, TProvider>();
        return this;
    }

    /// <summary>
    /// 自定义文档存储
    /// </summary>
    public SwaggerAggregationBuilder UseDocumentStore<TStore>()
        where TStore : class, IAggregatedDocumentStore
    {
        _services.RemoveAll<IAggregatedDocumentStore>();
        _services.AddSingleton<IAggregatedDocumentStore, TStore>();
        return this;
    }

    /// <summary>
    /// 自定义文档加载器
    /// </summary>
    public SwaggerAggregationBuilder UseDocumentLoader<TLoader>()
        where TLoader : class, ISwaggerDocumentLoader
    {
        _services.RemoveAll<ISwaggerDocumentLoader>();
        _services.AddSingleton<ISwaggerDocumentLoader, TLoader>();
        return this;
    }

    /// <summary>
    /// 自定义文档合并器
    /// </summary>
    public SwaggerAggregationBuilder UseDocumentMerger<TMerger>()
        where TMerger : class, ISwaggerDocumentMerger
    {
        _services.RemoveAll<ISwaggerDocumentMerger>();
        _services.AddSingleton<ISwaggerDocumentMerger, TMerger>();
        return this;
    }

    /// <summary>
    /// 配置选项
    /// </summary>
    public SwaggerAggregationBuilder Configure(Action<SwaggerAggregationOptions> configure)
    {
        _services.Configure(configure);
        return this;
    }

    internal void Build()
    {
        foreach (var type in _transformerTypes)
        {
            _services.AddSingleton(typeof(ISwaggerDocumentTransformer), type);
        }
    }
}
