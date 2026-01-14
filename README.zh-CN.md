# Yuzhu.Yarp.Swagger

[![NuGet](https://img.shields.io/nuget/v/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

中文 | [English](README.md)

Swagger 文档聚合库，用于 YARP（Yet Another Reverse Proxy）。自动发现并聚合后端服务的 Swagger 文档。

## 目录

- [功能特性](#功能特性)
- [前置条件](#前置条件)
- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [高级配置](#高级配置)
- [故障排查](#故障排查)
- [架构说明](#架构说明)

## 功能特性

- **后台自动刷新** - 异步加载文档，可配置刷新间隔
- **YARP 元数据发现** - 通过 YARP Cluster 元数据配置 Swagger 端点（DRY 原则）
- **OAuth2 支持** - 内置客户端凭证流支持（基于 Duende.AccessTokenManagement）
- **弹性策略** - 基于 Polly 的重试、熔断和超时策略
- **遥测支持** - OpenTelemetry 指标和分布式追踪
- **路径过滤** - 支持正则表达式路径过滤
- **文档分组** - 将多个服务分组到不同的 Swagger 文档

## 前置条件

在使用本库之前，请确保：

| 要求 | 版本 |
|------|------|
| .NET | 10.0 或更高 |
| YARP | 2.3.0 或更高 |
| Swashbuckle.AspNetCore | 10.1.0 或更高 |

**你需要了解的基础知识：**

1. **YARP（Yet Another Reverse Proxy）** - 微软的反向代理库
   - 官方文档：<https://microsoft.github.io/reverse-proxy/>
   - 入门教程：<https://microsoft.github.io/reverse-proxy/articles/getting-started.html>

2. **Swagger/OpenAPI** - API 文档规范
   - ASP.NET Core 集成：<https://learn.microsoft.com/zh-cn/aspnet/core/tutorials/getting-started-with-swashbuckle>

## 快速开始

### 第一步：安装 NuGet 包

```bash
dotnet add package Yuzhu.Yarp.Swagger
```

### 第二步：配置 appsettings.json

以下是一个最简配置示例（单个后端服务）：

```json
{
  "ReverseProxy": {
    "Routes": {
      "ApiRoute": {
        "ClusterId": "ApiCluster",
        "Match": {
          "Path": "/api/{**catch-all}"
        },
        "Transforms": [
          { "PathPattern": "{**catch-all}" }
        ]
      }
    },
    "Clusters": {
      "ApiCluster": {
        "Destinations": {
          "Default": {
            "Address": "https://localhost:5001"
          }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Prefix": "/api"
        }
      }
    }
  }
}
```

### 第三步：配置 Program.cs

```csharp
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加 Swagger 服务
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. 添加 YARP 反向代理和 Swagger 聚合
var configuration = builder.Configuration.GetSection("ReverseProxy");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation();  // 添加这一行即可启用 Swagger 聚合

var app = builder.Build();

// 3. 启用 Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.ConfigureAggregatedEndpoints(app.Services);  // 配置聚合端点
});

app.MapReverseProxy();

app.Run();
```

### 第四步：运行并验证

1. 启动你的后端服务（确保它有 Swagger 文档）
2. 启动 YARP 网关
3. 访问 `https://localhost:<port>/swagger` 查看聚合后的 Swagger UI

![Swagger UI 效果](https://raw.githubusercontent.com/andreytreyt/yarp-swagger/main/README.png)

## 配置说明

### Cluster 元数据选项

在 YARP 的 Cluster 配置中，通过 `Metadata` 配置 Swagger 发现：

| 元数据键 | 说明 | 默认值 |
|----------|------|--------|
| `Swagger:Enabled` | 是否启用此 Cluster 的 Swagger 聚合 | `false` |
| `Swagger:Path` | Swagger JSON 文档路径 | `/swagger/v1/swagger.json` |
| `Swagger:Prefix` | 添加到所有操作的路径前缀 | (无) |
| `Swagger:PathFilter` | 用于过滤路径的正则表达式（最大 500 字符） | (无) |
| `Swagger:OnlyPublishedPaths` | 仅包含已发布的路径 | `false` |
| `Swagger:IsMetadataSource` | 使用此 Cluster 的文档信息作为元数据来源 | `false` |
| `Swagger:AccessTokenClient` | OAuth2 客户端名称（用于认证） | (无) |
| `Swagger:DocumentName` | 文档分组名称（默认使用 ClusterId） | (cluster id) |

### 聚合选项

在 `appsettings.json` 中通过 `SwaggerAggregation` 节点配置：

```json
{
  "SwaggerAggregation": {
    "RefreshInterval": "00:05:00",
    "LoadTimeout": "00:00:30",
    "MaxRetryAttempts": 3
  }
}
```

| 选项 | 说明 | 默认值 | 范围 |
|------|------|--------|------|
| `RefreshInterval` | 后台刷新间隔 | `00:05:00` | 10秒 - 24小时 |
| `LoadTimeout` | 加载单个 Swagger 文档的超时时间 | `00:00:30` | 5秒 - 5分钟 |
| `AggregationTimeout` | 整体聚合过程超时时间 | `00:02:00` | 30秒 - 10分钟 |
| `MaxParallelism` | 最大并行加载数 | `10` | 1 - 50 |
| `MaxRetryAttempts` | 失败重试次数 | `3` | 0 - 10 |
| `DefaultSwaggerPath` | 默认 Swagger 路径 | `/swagger/v1/swagger.json` | 最大 200 字符 |
| `StartupDelay` | 首次刷新前的延迟 | `00:00:05` | - |
| `MaxDocumentSizeBytes` | 最大文档大小 | `10485760` (10MB) | 1KB - 100MB |
| `MergeIntoSingleDocument` | 是否合并为单个文档 | `false` | - |
| `DocumentName` | 合并后的文档名称 | - | - |
| `SchemaConflictStrategy` | Schema 冲突策略 | `FirstWins` | - |

## 多服务配置示例

以下示例展示如何聚合多个后端服务的 Swagger 文档：

```json
{
  "ReverseProxy": {
    "Routes": {
      "App1Route": {
        "ClusterId": "App1Cluster",
        "Match": { "Path": "/proxy-app1/{**catch-all}" },
        "Transforms": [{ "PathPattern": "{**catch-all}" }]
      },
      "App2Route": {
        "ClusterId": "App2Cluster",
        "Match": { "Path": "/proxy-app2/{**catch-all}" },
        "Transforms": [{ "PathPattern": "{**catch-all}" }]
      }
    },
    "Clusters": {
      "App1Cluster": {
        "Destinations": {
          "Default": { "Address": "https://localhost:5101" }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Prefix": "/proxy-app1",
          "Swagger:IsMetadataSource": "true"
        }
      },
      "App2Cluster": {
        "Destinations": {
          "Default": { "Address": "https://localhost:5102" }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Prefix": "/proxy-app2",
          "Swagger:IsMetadataSource": "true"
        }
      }
    }
  }
}
```

> **注意：** `Swagger:IsMetadataSource` 选项控制是否使用该 Cluster 的 Swagger 文档信息（标题、版本、描述）显示在聚合后的 Swagger UI 中。如果不设置此选项，将显示默认标题 "Aggregated API"，而不是后端服务的实际标题。

## 认证配置

如果后端服务的 Swagger 端点需要认证，可以配置 OAuth2 客户端凭证：

### appsettings.json

```json
{
  "ReverseProxy": {
    "Clusters": {
      "App1Cluster": {
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Prefix": "/proxy-app1",
          "Swagger:AccessTokenClient": "Identity"
        }
      }
    }
  }
}
```

### Program.cs

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

注册自定义转换器来修改 Swagger 文档：

```csharp
public class MyCustomTransformer : ISwaggerDocumentTransformer
{
    public int Order => 100; // 执行顺序（数字越小越先执行）

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

### 自定义端点提供器

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

替换默认的内存存储（例如使用分布式缓存）：

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentStore<RedisDocumentStore>();
    });
```

### 编程式配置

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

## 遥测

本库在 `Yarp.Swagger.Aggregation` 源名称下发出 OpenTelemetry 指标和追踪。

### 指标

| 指标 | 类型 | 说明 |
|------|------|------|
| `swagger.refresh.count` | Counter | 刷新操作次数 |
| `swagger.load.success` | Counter | 成功加载文档次数 |
| `swagger.load.failure` | Counter | 失败加载文档次数 |
| `swagger.cache.hit` | Counter | 缓存命中次数 |
| `swagger.load.duration` | Histogram | 加载耗时（毫秒） |
| `swagger.refresh.duration` | Histogram | 刷新耗时（毫秒） |
| `swagger.endpoints.count` | Gauge | 发现的端点数量 |

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

## 故障排查

### 常见问题

#### 1. Swagger UI 没有显示聚合的端点

**可能原因：**

- 后端服务未启动或不可访问
- `Swagger:Enabled` 未设置为 `true`
- Swagger 路径配置错误

**解决方法：**

1. 确认后端服务正在运行
2. 检查后端服务的 Swagger 文档是否可以直接访问（如 `https://localhost:5001/swagger/v1/swagger.json`）
3. 确认 `Swagger:Enabled` 设置为 `"true"`（注意是字符串）

#### 2. 加载超时错误

**可能原因：**

- 后端服务响应慢
- 网络问题
- `LoadTimeout` 设置过短

**解决方法：**

```json
{
  "SwaggerAggregation": {
    "LoadTimeout": "00:01:00"
  }
}
```

#### 3. 认证失败

**可能原因：**

- OAuth2 客户端配置错误
- Token 端点不可访问
- 客户端凭证无效

**解决方法：**

1. 验证 Identity Server 是否运行
2. 检查 `ClientId` 和 `ClientSecret` 是否正确
3. 确认 Token 端点 URL 正确

#### 4. 路径前缀不正确

**可能原因：**

- `Swagger:Prefix` 配置与 YARP 路由不匹配

**解决方法：**

确保 `Swagger:Prefix` 与 YARP Route 的 `Match.Path` 前缀一致：

```json
{
  "Routes": {
    "ApiRoute": {
      "Match": { "Path": "/api/{**catch-all}" }
    }
  },
  "Clusters": {
    "ApiCluster": {
      "Metadata": {
        "Swagger:Prefix": "/api"
      }
    }
  }
}
```

### 调试技巧

启用详细日志以排查问题：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Yuzhu.Yarp.Swagger": "Debug"
    }
  }
}
```

## 架构说明

```text
src/Yuzhu.Yarp.Swagger/
├── Abstractions/           # 核心接口 (ISwaggerAggregator, ISwaggerDocumentLoader 等)
├── Adapters/Swashbuckle/   # Swashbuckle 集成 (AggregatedSwaggerProvider)
├── Background/             # 后台刷新服务 (SwaggerRefreshService, SwaggerAggregator)
├── Configuration/          # 配置选项 (SwaggerAggregationOptions, MetadataKeys)
├── Discovery/              # 端点发现 (ConfigBasedSwaggerEndpointProvider)
├── Extensions/             # 服务注册 (AddSwaggerAggregation)
├── Loading/                # HTTP 文档加载器（带弹性策略）
├── Merging/                # 文档合并器 (paths, components, tags)
├── Resilience/             # 基于 Polly 的重试、熔断、超时
├── Storage/                # 内存文档存储
├── Telemetry/              # OpenTelemetry 指标和追踪
└── Transforming/           # 路径前缀和过滤转换器
```

### 数据流程

```text
┌─────────────────────────────────────────────────────────────┐
│                        应用启动                              │
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
│ - 从 YARP 配置    │   endpoints     │ - OAuth2 令牌         │
│   发现端点        │                 │ - 重试/熔断           │
└───────────────────┘                 └───────────────────────┘
                                                  │
                                                  ▼ documents
                                      ┌───────────────────────┐
                                      │ ISwaggerDocument-     │
                                      │ Transformer[]         │
                                      │ - 路径前缀转换         │
                                      │ - 路径过滤转换         │
                                      └───────────────────────┘
                                                  │
                                                  ▼ transformed
                                      ┌───────────────────────┐
                                      │ ISwaggerDocumentMerger│
                                      │ - 合并路径            │
                                      │ - 合并组件            │
                                      └───────────────────────┘
                                                  │
                                                  ▼ merged
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

本项目基于 MIT 许可证开源。
