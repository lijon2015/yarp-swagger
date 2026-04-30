# Yuzhu.Yarp.Swagger

[![NuGet](https://img.shields.io/nuget/v/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

中文 | [English](README.md)

面向 YARP 的 Swagger / OpenAPI 聚合库。通过可插拔的 endpoint source 与
address resolver，从 YARP 运行时状态与配置中发现下游 Swagger 文档；
为 `/swagger/{document}/swagger.json` 提供明确的 `200`/`404`/`503` 语义；
并基于 YARP 配置变更触发刷新。

## 安装与最小用例

```csharp
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

IReverseProxyBuilder reverseProxy = builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
    reverseProxy.AddSwaggerAggregation();
}

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerAggregationDocuments(); // 200 / 404 / 503 语义
    app.UseSwagger();
    app.UseSwaggerUI(o => o.ConfigureAggregatedEndpoints(app.Services));
}

app.MapReverseProxy();
app.Run();
```

YARP 集群上启用聚合：

```jsonc
{
  "ReverseProxy": {
    "Clusters": {
      "OrdersCluster": {
        "Destinations": {
          "Default": { "Address": "https://orders.internal" }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Path": "/swagger/v1/swagger.json",
          "Swagger:Prefix": "/proxy-orders",
          "Swagger:DocumentName": "orders",
          "Swagger:IsMetadataSource": "true"
        }
      }
    }
  }
}
```

## 元数据键

| 键 | 作用 |
| --- | ---- |
| `Swagger:Enabled` | 启用聚合（取字面量 `"true"`）。 |
| `Swagger:Path` | 覆盖下游 Swagger 文档路径。 |
| `Swagger:Prefix` | 转换阶段为所有 path 增加前缀。 |
| `Swagger:PathFilter` | 转换阶段保留匹配该正则的 path。 |
| `Swagger:DocumentName` | 聚合文档分组名，缺省时回退到 cluster id。 |
| `Swagger:IsMetadataSource` | 该 cluster 的 `info` 用于合并文档。 |
| `Swagger:AccessTokenClient` | OAuth2 客户端凭证客户端名。 |

## HTTP 语义

`UseSwaggerAggregationDocuments()` 监听 `/{RoutePrefix}/{documentName}/swagger.json`，
并产生确定性的响应：

| 场景 | 状态码 | 响应体 |
| ---- | ------ | ------ |
| 文档可解析（缓存或现场聚合） | `200` | OpenAPI JSON，按 `OpenApiSpecVersion` 序列化。 |
| 请求名不在发现快照中 | `404` | `application/problem+json`，列出请求名与已知文档名。 |
| 文档已发现但所有后端加载失败 | `503`（可配置） | `application/problem+json`，原因汇总为 `cluster\|stage\|status`。 |
| 路径不匹配 | 透传 | 由后续 middleware 处理。 |

部分后端失败仍返回 `200`：成功加载的部分会合并到文档中，失败通过日志和指标
暴露，不会因此抑制响应。

中间件必须在 `UseSwagger()` **之前**注册，确保它的语义优先于 Swashbuckle 默认的
异常处理（默认会把聚合失败转为 500）。

## 聚合文档端点选项

`SwaggerAggregationDocumentEndpointOptions` 通过 `builder.ConfigureDocumentEndpoint(...)`
配置：

```csharp
reverseProxy.AddSwaggerAggregation(builder => builder
    .ConfigureDocumentEndpoint(o =>
    {
        o.RoutePrefix = "/swagger";
        o.UnavailableStatusCode = StatusCodes.Status503ServiceUnavailable;
        o.IncludeKnownDocumentsOnNotFound = true;
        o.OpenApiSpecVersion = OpenApiSpecVersion.OpenApi3_0;
        o.DiagnosticsDocumentName = "diagnostics";
    }));
```

| 选项 | 默认值 | 说明 |
| ---- | ------ | ---- |
| `RoutePrefix` | `/swagger` | 中间件监听的路由前缀。 |
| `UnavailableStatusCode` | `503` | 已知文档无法聚合时返回的状态码。 |
| `IncludeKnownDocumentsOnNotFound` | `true` | 是否在 404 响应体中列出已知文档。 |
| `OpenApiSpecVersion` | `OpenApi3_0` | 序列化合并文档使用的 OpenAPI 版本。 |
| `DiagnosticsDocumentName` | `diagnostics` | 诊断端点对应的文档名。 |

## 扩展点

```csharp
reverseProxy.AddSwaggerAggregation(builder => builder
    .AddSource<MyServiceDirectorySource>()
    .InsertAddressResolver<GatewayFallbackDestinationAddressResolver>()
    .AddRefreshTrigger<ConsulServiceWatcherRefreshTrigger>()
    .AddTransformer<MyTagRewriteTransformer>()
    .UseDocumentStore<RedisAggregatedDocumentStore>()
    .Configure(o => o.RefreshInterval = TimeSpan.FromMinutes(2))
    .ConfigureDocumentEndpoint(o => o.UnavailableStatusCode = 502));
```

### 空发现 UI 行为

发现结果为空时，`AggregatedSwaggerUIOptions.EmptyBehavior` 决定 UI 下拉行为：

- `NoEndpoints`（默认）— 下拉为空，输出结构化告警日志。
- `DiagnosticEndpoint` — 下拉只展示一个 "Swagger Aggregation Diagnostics"，
  指向 `/{RoutePrefix}/{DiagnosticsDocumentName}/swagger.json`。中间件会在该
  路径返回一个真正的 OpenAPI 文档，`info.description` 中包含当前发现诊断，
  Swagger UI 可以正常渲染，而不会再次出现 "failed to load"。

UI 端的 `RoutePrefix` 与 `DiagnosticsDocumentName` 默认从
`SwaggerAggregationDocumentEndpointOptions` 取值，无需手动同步。

## 诊断与可观测性

`SwaggerEndpointDiscoveryResult` 同时返回成功的 endpoints 和每个 cluster
被跳过 / 失败的结构化诊断（`ClusterId` / `Stage` / `Severity` / `Message`）。
`SwaggerRefreshService` 在每次刷新结束时记录这些诊断；`UseSwaggerAggregationDocuments`
在 404 响应里同时返回当前已知的 document 列表。

OpenTelemetry 信号（source：`Yuzhu.Yarp.Swagger`）：

| 类型 | 名称 |
| ---- | ---- |
| Activity | `SwaggerDiscovery` / `LoadSwaggerDocument` / `AggregateDocuments` |
| Counter | `swagger.discovery.skipped` / `swagger.load.success` / `swagger.load.failure` / `swagger.cache.hit` |
| Histogram | `swagger.load.duration` / `swagger.refresh.duration`（ms） |
| Gauge | `swagger.endpoints.count` |

公共 tag：`cluster.id`、`document.name`、`destination.address`、`swagger.path`、
`http.status_code`、`failure.stage`、`failure.reason`、`from.cache`。

## OpenAPI 版本兼容

| 组件 | 版本 | 说明 |
| ---- | ---- | ---- |
| OpenAPI Specification（参考） | 3.0 / 3.1 / 3.2 | 当前 latest 为 3.2。 |
| `Microsoft.OpenApi 2.4.1`（间接依赖） | 3.0 / 3.1 | 仅暴露 3.0 与 3.1；尚未支持 3.2 序列化。 |
| Swashbuckle.AspNetCore 10.1.7 | 3.0 / 3.1 | provider/middleware 行为按该版本验证。 |
| Swagger UI | 3.0 / 3.1 / 3.2 | 3.2 渲染需 .NET 链路先支持 3.2 序列化。 |

策略：

- 解析阶段尽量保留下游文档的字段。
- 序列化版本由 `SwaggerAggregationDocumentEndpointOptions.OpenApiSpecVersion`
  控制，默认 `OpenApi3_0` 以兼顾最广的工具链。
- 下游为 3.1 时可设置为 `OpenApi3_1` 保留线格式。
- 库内不做 per-document 版本保留；如需为不同文档输出不同 wire 版本，请实现
  自定义 merger。

## License

MIT — 参见 [LICENSE](LICENSE.md)。
