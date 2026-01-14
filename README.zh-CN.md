# YARP Swagger 聚合

![dotnet_main](https://github.com/andreytreyt/yarp-swagger/actions/workflows/dotnet.yml/badge.svg?branch=main)
![release](https://github.com/andreytreyt/yarp-swagger/actions/workflows/release.yml/badge.svg)
[![nuget_v](https://img.shields.io/nuget/v/Treyt.Yarp.ReverseProxy.Swagger?logo=nuget)](https://www.nuget.org/packages/Treyt.Yarp.ReverseProxy.Swagger/)
![nuget_dt](https://img.shields.io/nuget/dt/Treyt.Yarp.ReverseProxy.Swagger?logo=nuget)

YARP（Yet Another Reverse Proxy）的 Swagger 聚合库。自动从后端服务发现并聚合 Swagger 文档，通过 YARP 集群元数据进行配置。

## 功能特性

- **后台刷新** - 异步文档加载，支持可配置的刷新间隔
- **YARP 元数据发现** - 通过 YARP 集群元数据配置 Swagger 端点（DRY 原则）
- **OAuth2 支持** - 内置客户端凭证流支持，基于 Duende.AccessTokenManagement
- **弹性处理** - 基于 Polly 的重试、熔断器和超时策略
- **遥测监控** - OpenTelemetry 指标和分布式追踪
- **路径过滤** - 基于正则表达式的路径过滤，带 ReDoS 防护
- **文档分组** - 将多个服务分组到不同的 Swagger 文档

## 快速开始

首先为项目配置 [Swagger](https://learn.microsoft.com/zh-cn/aspnet/core/tutorials/getting-started-with-swashbuckle) 和 [YARP](https://microsoft.github.io/reverse-proxy/articles/getting-started.html)。

### 安装

```bash
dotnet add package Treyt.Yarp.ReverseProxy.Swagger
```

### 配置

更新 `appsettings.json`：

```json
{
  "SwaggerAggregation": {
    "RefreshInterval": "00:05:00",
    "LoadTimeout": "00:00:30",
    "MaxRetryAttempts": 3
  },
  "ReverseProxy": {
    "Routes": {
      "App1Route": {
        "ClusterId": "App1Cluster",
        "Match": {
          "Path": "/proxy-app1/{**catch-all}"
        },
        "Transforms": [
          { "PathPattern": "{**catch-all}" }
        ]
      },
      "App2Route": {
        "ClusterId": "App2Cluster",
        "Match": {
          "Path": "/proxy-app2/{**catch-all}"
        },
        "Transforms": [
          { "PathPattern": "{**catch-all}" }
        ]
      }
    },
    "Clusters": {
      "App1Cluster": {
        "Destinations": {
          "Default": {
            "Address": "https://localhost:5101"
          }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Path": "/swagger/v1/swagger.json",
          "Swagger:Prefix": "/proxy-app1"
        }
      },
      "App2Cluster": {
        "Destinations": {
          "Default": {
            "Address": "https://localhost:5102"
          }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Path": "/swagger/v1/swagger.json",
          "Swagger:Prefix": "/proxy-app2"
        }
      }
    }
  }
}
```

更新 `Program.cs`：

```csharp
using Yarp.ReverseProxy.Swagger.Adapters.Swashbuckle;
using Yarp.ReverseProxy.Swagger.Extensions;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration.GetSection("ReverseProxy");

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation();

builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.ConfigureAggregatedEndpoints(app.Services);
});

app.MapReverseProxy();

app.Run();
```

完成！Swagger UI 将自动显示所有配置的后端服务的聚合端点。

![image](https://raw.githubusercontent.com/andreytreyt/yarp-swagger/main/README.png)

## 集群元数据选项

通过 YARP 集群元数据配置 Swagger 发现：

| 元数据键                     | 描述                                    | 默认值                      |
| --------------------------- | --------------------------------------- | -------------------------- |
| `Swagger:Enabled`           | 为此集群启用 Swagger 聚合                 | `false`                    |
| `Swagger:Path`              | Swagger JSON 文档路径                    | `/swagger/v1/swagger.json` |
| `Swagger:Prefix`            | 添加到所有操作的路径前缀                   | （无）                      |
| `Swagger:PathFilter`        | 路径过滤正则表达式（最大 500 字符）         | （无）                      |
| `Swagger:OnlyPublishedPaths`| 仅包含已发布的路径                        | `false`                    |
| `Swagger:IsMetadataSource`  | 使用此集群的文档信息作为元数据源            | `false`                    |
| `Swagger:AccessTokenClient` | 用于身份验证的 OAuth2 客户端名称           | （无）                      |
| `Swagger:DocumentName`      | 用于分组的文档名称（默认为集群 ID）         | （集群 ID）                 |

## 聚合选项

通过 `appsettings.json` 中的 `SwaggerAggregation` 节进行配置：

| 选项                  | 描述                          | 默认值                      | 范围           |
| -------------------- | ----------------------------- | -------------------------- | -------------- |
| `RefreshInterval`    | 后台刷新间隔                    | `00:05:00`                 | 10秒 - 24小时   |
| `LoadTimeout`        | 加载单个 Swagger 文档的超时时间  | `00:00:30`                 | 5秒 - 5分钟     |
| `AggregationTimeout` | 整体聚合过程超时时间             | `00:02:00`                 | 30秒 - 10分钟   |
| `MaxParallelism`     | 最大并行加载数                  | `10`                       | 1 - 50         |
| `MaxRetryAttempts`   | 加载失败最大重试次数             | `3`                        | 0 - 10         |
| `DefaultSwaggerPath` | 元数据未指定时的默认 Swagger 路径 | `/swagger/v1/swagger.json` | 最大 200 字符   |
| `StartupDelay`       | 首次刷新前的初始延迟             | `00:00:05`                 | -              |
| `MaxDocumentSizeBytes` | 最大文档大小，防止内存溢出      | `10485760`（10MB）          | 1KB - 100MB    |

## 身份验证

对于受保护的 Swagger 端点，配置 OAuth2 客户端凭证：

更新 `appsettings.json`：

```json
{
  "ReverseProxy": {
    "Clusters": {
      "App1Cluster": {
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Path": "/swagger/v1/swagger.json",
          "Swagger:Prefix": "/proxy-app1",
          "Swagger:AccessTokenClient": "Identity"
        }
      }
    }
  }
}
```

更新 `Program.cs`：

```csharp
builder.Services.AddClientCredentialsTokenManagement()
    .AddClient("Identity", client =>
    {
        client.TokenEndpoint = "https://identity-server/connect/token";
        client.ClientId = "your-client-id";
        client.ClientSecret = "your-client-secret";
    });

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation();
```

## 高级配置

### 自定义文档转换器

注册自定义转换器以修改 Swagger 文档：

```csharp
public class MyCustomTransformer : ISwaggerDocumentTransformer
{
    public int Order => 100; // 执行顺序（数值越小越先执行）

    public ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 在此修改文档
        return ValueTask.FromResult(document);
    }
}

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.AddTransformer<MyCustomTransformer>();
    });
```

### 自定义端点提供者

替换默认的基于配置的端点发现：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseEndpointProvider<MyCustomEndpointProvider>();
    });
```

### 自定义文档加载器

替换默认的基于 HTTP 的文档加载器：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentLoader<MyCustomDocumentLoader>();
    });
```

### 自定义文档存储

替换默认的内存文档存储（例如用于分布式缓存）：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentStore<RedisDocumentStore>();
    });
```

### 自定义文档合并器

替换默认的文档合并器：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentMerger<MyCustomMerger>();
    });
```

### 代码配置

通过代码配置选项：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.Configure(options =>
        {
            options.RefreshInterval = TimeSpan.FromMinutes(10);
            options.LoadTimeout = TimeSpan.FromSeconds(60);
            options.MaxRetryAttempts = 5;
        });
    });
```

## 遥测监控

该库在源名称 `Yarp.Swagger.Aggregation` 下发出 OpenTelemetry 指标和追踪。

### 指标

| 指标                         | 类型      | 描述                |
| --------------------------- | --------- | ------------------- |
| `swagger.refresh.count`     | Counter   | 刷新操作次数         |
| `swagger.load.success`      | Counter   | 成功加载文档次数      |
| `swagger.load.failure`      | Counter   | 加载文档失败次数      |
| `swagger.cache.hit`         | Counter   | 缓存命中次数         |
| `swagger.load.duration`     | Histogram | 加载耗时（毫秒）      |
| `swagger.refresh.duration`  | Histogram | 刷新耗时（毫秒）      |
| `swagger.endpoints.count`   | Gauge     | 发现的端点数量        |

### 追踪

- `LoadSwaggerDocument` - 每个文档加载操作的 Activity
- `AggregateSwaggerDocuments` - 聚合过程的 Activity

### 启用遥测

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Yarp.Swagger.Aggregation");
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("Yarp.Swagger.Aggregation");
    });
```

## 架构

```text
src/Yarp.ReverseProxy.Swagger/
├── Abstractions/           # 核心接口（ISwaggerAggregator、ISwaggerDocumentLoader 等）
├── Adapters/Swashbuckle/   # Swashbuckle 集成（AggregatedSwaggerProvider）
├── Background/             # 后台刷新服务（SwaggerRefreshService、SwaggerAggregator）
├── Configuration/          # 选项和构建器（SwaggerAggregationOptions、MetadataKeys）
├── Discovery/              # 端点发现（ConfigBasedSwaggerEndpointProvider）
├── Extensions/             # 服务注册（AddSwaggerAggregation）
├── Loading/                # 带弹性处理的 HTTP 文档加载器
├── Merging/                # 文档合并器（路径、组件、标签）
├── Resilience/             # 基于 Polly 的重试、熔断器、超时
├── Storage/                # 内存文档存储
├── Telemetry/              # OpenTelemetry 指标和追踪
└── Transforming/           # 路径前缀和过滤转换器
```

### 核心组件

| 组件                              | 接口                           | 描述                           |
| -------------------------------- | ----------------------------- | ------------------------------ |
| `ConfigBasedSwaggerEndpointProvider` | `ISwaggerEndpointProvider` | 从 YARP 配置发现端点             |
| `HttpSwaggerDocumentLoader`      | `ISwaggerDocumentLoader`      | 通过 HTTP 加载 Swagger 文档，支持 OAuth2 |
| `DefaultSwaggerDocumentMerger`   | `ISwaggerDocumentMerger`      | 合并多个 Swagger 文档            |
| `SwaggerAggregator`              | `ISwaggerAggregator`          | 协调加载、转换、合并流程          |
| `InMemoryAggregatedDocumentStore` | `IAggregatedDocumentStore`   | 线程安全的内存文档缓存            |
| `SwaggerRefreshService`          | `IHostedService`              | 定期刷新的后台服务               |
| `AggregatedSwaggerProvider`      | `IAsyncSwaggerProvider`       | Swashbuckle 集成，支持按需加载   |
| `PathPrefixTransformer`          | `ISwaggerDocumentTransformer` | 为操作添加路径前缀               |
| `PathFilterTransformer`          | `ISwaggerDocumentTransformer` | 使用正则表达式过滤路径            |

### 数据流

```text
┌─────────────────────────────────────────────────────────────┐
│                        应用程序启动                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
            ┌─────────────────────────────────────┐
            │   AddSwaggerAggregation()           │
            │   - 注册服务                         │
            │   - 配置 HttpClient                 │
            │   - 启动后台服务                     │
            └─────────────────────────────────────┘
                              │
                              ▼
            ┌─────────────────────────────────────┐
            │   SwaggerRefreshService             │
            │   - 定期刷新                         │
            │   - 配置变更检测                     │
            └─────────────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        ▼                                           ▼
┌───────────────────┐                 ┌───────────────────────┐
│ ISwaggerEndpoint- │                 │ ISwaggerDocumentLoader│
│ Provider          │────────────────▶│ - HTTP 请求           │
│ - 从 YARP 配置    │    端点列表      │ - OAuth2 令牌         │
│   发现端点        │                 │ - 重试/熔断           │
└───────────────────┘                 └───────────────────────┘
                                                  │
                                                  ▼ 文档
                                      ┌───────────────────────┐
                                      │ ISwaggerDocument-     │
                                      │ Transformer[]         │
                                      │ - 路径前缀转换         │
                                      │ - 路径过滤转换         │
                                      └───────────────────────┘
                                                  │
                                                  ▼ 已转换
                                      ┌───────────────────────┐
                                      │ ISwaggerDocumentMerger│
                                      │ - 合并路径            │
                                      │ - 合并组件            │
                                      └───────────────────────┘
                                                  │
                                                  ▼ 已合并
                                      ┌───────────────────────┐
                                      │ IAggregatedDocument-  │
                                      │ Store                 │
                                      │ - 内存缓存            │
                                      └───────────────────────┘
                                                  │
                                                  ▼
            ┌─────────────────────────────────────┐
            │   GET /swagger/{doc}/swagger.json   │
            │   AggregatedSwaggerProvider         │
            │   - 缓存查找                         │
            │   - 按需加载                         │
            └─────────────────────────────────────┘
```

## 许可证

本项目基于 Apache 2.0 许可证授权。
