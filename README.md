# Yuzhu.Yarp.Swagger

[![NuGet](https://img.shields.io/nuget/v/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[中文文档](README.zh-CN.md) | English

Swagger / OpenAPI aggregation for YARP. Discovers backend Swagger endpoints
from YARP runtime state and configuration through pluggable sources and
address resolvers, aggregates documents with explicit `200` / `404` / `503`
semantics, and refreshes on YARP config changes.

## Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [HTTP semantics](#http-semantics)
- [Architecture](#architecture)
- [Extension points](#extension-points)
- [Diagnostics and observability](#diagnostics-and-observability)
- [OpenAPI version compatibility](#openapi-version-compatibility)
- [Troubleshooting](#troubleshooting)

## Features

- **Discovery pipeline** — `ISwaggerEndpointSource` + `ISwaggerEndpointAddressResolver` +
  `ISwaggerMetadataAccessor` with structured per-cluster diagnostics.
- **Standard YARP coverage out of the box** — runtime cluster state and static
  configuration are both supported as default sources.
- **Event-driven refresh** — refreshes fire on YARP config change tokens,
  options changes, and a periodic timer.
- **Strict UI semantics** — Swagger UI advertises only documents that the
  discovery pipeline actually found; no fake `/swagger/v1/swagger.json`.
- **Library-owned `/swagger/{name}/swagger.json` middleware** — explicit 200,
  404, and 503 responses instead of opaque 500s.
- **OAuth2** — client-credentials access tokens via `Duende.AccessTokenManagement`.
- **Resilience** — Polly retry / per-attempt timeout / circuit breaker.
- **Telemetry** — OpenTelemetry metrics and tracing with documented tag names.

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET | 10.0 |
| YARP | 2.3.0+ |
| Swashbuckle.AspNetCore | 10.1.0+ |
| Microsoft.OpenApi | 2.4.1+ (transitive) |

## Quick start

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
    // 200 / 404 / 503 semantics for /swagger/{document}/swagger.json
    app.UseSwaggerAggregationDocuments();

    // Standard Swashbuckle middleware (kept for compatibility tooling)
    app.UseSwagger();
    app.UseSwaggerUI(options => options.ConfigureAggregatedEndpoints(app.Services));
}

app.MapReverseProxy();
app.Run();
```

YARP cluster metadata enables aggregation per cluster:

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

## Configuration

### `SwaggerAggregation` section

```jsonc
{
  "SwaggerAggregation": {
    "RefreshInterval": "00:05:00",
    "LoadTimeout": "00:00:30",
    "AggregationTimeout": "00:02:00",
    "MaxParallelism": 10,
    "MaxRetryAttempts": 3,
    "DefaultSwaggerPath": "/swagger/v1/swagger.json",
    "StartupDelay": "00:00:05",
    "MaxDocumentSizeBytes": 10485760,
    "IncludeFailedServicesWarning": false
  }
}
```

### Cluster metadata keys

| Key | Purpose |
| --- | ------- |
| `Swagger:Enabled` | Enable aggregation for the cluster (`"true"`). |
| `Swagger:Path` | Override the Swagger document path on the backend. |
| `Swagger:Prefix` | Prefix prepended to every transformed path. |
| `Swagger:PathFilter` | Regex used to filter the paths kept after transform. |
| `Swagger:DocumentName` | Logical document group name. Falls back to cluster id. |
| `Swagger:IsMetadataSource` | This cluster's `info` block wins for the merged document. |
| `Swagger:AccessTokenClient` | OAuth2 client credentials client name. |

### Aggregation document endpoint

`UseSwaggerAggregationDocuments()` is configured through
`SwaggerAggregationDocumentEndpointOptions`. Set values via
`builder.ConfigureDocumentEndpoint(...)` during DI registration:

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

| Option | Default | Purpose |
| ------ | ------- | ------- |
| `RoutePrefix` | `/swagger` | Path prefix the middleware listens on. |
| `UnavailableStatusCode` | `503` | Status returned when a known document fails to aggregate. |
| `IncludeKnownDocumentsOnNotFound` | `true` | Include known document names in the 404 problem body. |
| `OpenApiSpecVersion` | `OpenApi3_0` | Wire format used when serializing the merged document. |
| `DiagnosticsDocumentName` | `diagnostics` | Document name served by the optional diagnostics endpoint. |

## HTTP semantics

`UseSwaggerAggregationDocuments()` listens at
`/{RoutePrefix}/{documentName}/swagger.json` and produces deterministic
responses:

| Outcome | Status | Body |
| ------- | ------ | ---- |
| Document resolved (cache hit or fresh aggregation) | `200` | OpenAPI document, JSON, serialized at `OpenApiSpecVersion`. |
| Document name not in the discovery snapshot | `404` | `application/problem+json` with the requested name and known names. |
| Document discovered but every backend load failed | `503` (configurable) | `application/problem+json` with the failure reason summarized as `cluster\|stage\|status`. |
| Path doesn't match `/{prefix}/{name}/swagger.json` | passthrough | The next middleware decides. |

Partial backend failures still produce `200`: the merged document carries
whatever loaded successfully, and the failures are surfaced through logs and
metrics, not by suppressing the response.

The middleware must run **before** `UseSwagger()` so its semantics take
precedence over Swashbuckle's default exception handling (which would turn an
aggregation failure into an opaque 500).

## Architecture

```text
┌─────────────────────┐  GetCandidatesAsync ┌──────────────────────┐
│  EndpointSources    │ ───────────────►   │                      │
│  - YarpRuntime      │                    │ DiscoveryService     │
│  - YarpConfiguration│                    │  - dedup by cluster  │
└─────────────────────┘                    │  - read metadata     │
                                            │  - run resolvers     │
┌─────────────────────┐  ResolveAsync      │                      │
│ AddressResolvers    │ ◄──────────────── │  → Endpoints +       │
│  - YarpRuntime      │                    │    Diagnostics       │
│  - YarpConfigured   │                    └──────────┬───────────┘
└─────────────────────┘                               │
                                                      ▼
                                      ┌────────────────────────────┐
                                      │  SwaggerDocumentCoordinator│
                                      │  → load → transform → merge│
                                      └────────┬─────────┬─────────┘
                                               │         │
                              UseSwaggerAggreg │         │ AggregatedSwagger
                              ationDocuments() │         │ Provider (Swashbuckle)
                              200 / 404 / 503  │         │ ISwaggerProvider
                                               ▼         ▼
                                            HTTP                                                Refresh service
                                                          ◄── ISwaggerRefreshTrigger
                                                              - Options change
                                                              - Periodic
                                                              - YARP config change
```

## Extension points

The five-interface pipeline lets you replace any single stage without forking
the library.

### Custom endpoint source

```csharp
reverseProxy.AddSwaggerAggregation(builder =>
    builder.AddSource<MyServiceDirectorySource>());
```

### Custom address resolver (e.g. project fallback destinations)

```csharp
reverseProxy.AddSwaggerAggregation(builder =>
    builder.InsertAddressResolver<GatewayFallbackDestinationAddressResolver>());
```

### Custom refresh trigger

```csharp
reverseProxy.AddSwaggerAggregation(builder =>
    builder.AddRefreshTrigger<ConsulServiceWatcherRefreshTrigger>());
```

### Custom transformer

```csharp
reverseProxy.AddSwaggerAggregation(builder =>
    builder.AddTransformer<MyTagRewriteTransformer>());
```

### Override storage / loader / merger

```csharp
reverseProxy.AddSwaggerAggregation(builder => builder
    .UseDocumentStore<RedisAggregatedDocumentStore>()
    .UseDocumentLoader<MyHttpSwaggerLoader>()
    .UseDocumentMerger<StrictDocumentMerger>());
```

### Empty-discovery UI behavior

When discovery returns zero documents, `AggregatedSwaggerUIOptions.EmptyBehavior`
controls what the UI dropdown shows:

- `NoEndpoints` (default) — empty dropdown, structured warning logged.
- `DiagnosticEndpoint` — single dropdown entry "Swagger Aggregation Diagnostics"
  pointing at `/{RoutePrefix}/{DiagnosticsDocumentName}/swagger.json`. The
  middleware serves a real OpenAPI document at that path whose `info.description`
  lists the current discovery diagnostics, so Swagger UI renders cleanly instead
  of showing "failed to load".

```csharp
app.UseSwaggerUI(options => options.ConfigureAggregatedEndpoints(
    app.Services,
    ui =>
    {
        ui.EmptyBehavior = EmptySwaggerEndpointBehavior.DiagnosticEndpoint;
        ui.PrimaryDocumentName = "orders";
    }));
```

The UI's `RoutePrefix` and `DiagnosticsDocumentName` default to the values from
`SwaggerAggregationDocumentEndpointOptions`, so the URL automatically matches
what the middleware serves.

## Diagnostics and observability

Discovery returns a structured result with per-cluster diagnostics — every
skip and failure has a `ClusterId` / `Stage` / `Severity` / `Message` record:

```csharp
ISwaggerEndpointDiscoveryService discovery = ...;
SwaggerEndpointDiscoveryResult result = await discovery.DiscoverAsync();

foreach (SwaggerEndpointDiagnostic d in result.Diagnostics)
{
    logger.LogInformation(
        "Cluster {ClusterId} {Stage}/{Severity}: {Message}",
        d.ClusterId, d.Stage, d.Severity, d.Message);
}
```

OpenTelemetry signals (source: `Yuzhu.Yarp.Swagger`):

| Type | Name | Notes |
| ---- | ---- | ----- |
| Activity | `SwaggerDiscovery` | Tags: `document.name`, `endpoints.count` |
| Activity | `LoadSwaggerDocument` | Tags: `cluster.id`, `destination.address`, `swagger.path` |
| Activity | `AggregateDocuments` | Tags: `document.name`, `endpoints.count` |
| Counter | `swagger.discovery.skipped` | Tags: `cluster.id`, `failure.stage` |
| Counter | `swagger.load.success` | Tags: `cluster.id`, `document.name` |
| Counter | `swagger.load.failure` | Tags: `cluster.id`, `failure.stage`, `http.status_code` |
| Counter | `swagger.cache.hit` | Tags: `document.name` |
| Histogram | `swagger.load.duration` | ms |
| Histogram | `swagger.refresh.duration` | ms |
| Gauge | `swagger.endpoints.count` | observable |

## OpenAPI version compatibility

| Component | Versions | Notes |
| --------- | -------- | ----- |
| OpenAPI Specification (reference) | 3.0 / 3.1 / 3.2 | Latest spec at `spec.openapis.org/oas/latest.html` is 3.2. |
| `Microsoft.OpenApi 2.4.1` (transitive) | 3.0 / 3.1 | `OpenApiSpecVersion` only exposes 3.0 and 3.1; 3.2 wire format is not yet emitted. |
| Swashbuckle.AspNetCore 10.1.7 | 3.0 / 3.1 | Provider/middleware behavior validated at this version. |
| Swagger UI | 3.0 / 3.1 / 3.2 | Per Swagger's 3.2 announcement; 3.2 rendering only matters once the .NET stack can emit 3.2. |

Defaults and policy:

- The aggregator parses every downstream document with `Microsoft.OpenApi`,
  preserving fields that the underlying parser supports.
- The wire format used to serialize the merged document is controlled by
  `SwaggerAggregationDocumentEndpointOptions.OpenApiSpecVersion` (default
  `OpenApi3_0` for the widest tooling interop).
- Set `OpenApiSpecVersion = OpenApiSpecVersion.OpenApi3_1` if downstream
  services emit 3.1 and you need that wire format preserved.
- Per-document version preservation (i.e. emitting different wire formats per
  aggregated document) is not built in; supply a custom merger if you need it.

## Troubleshooting

### Swagger UI dropdown is empty

The library does not register a placeholder document. Check the structured
discovery diagnostics in the refresh service log line:

```text
Swagger discovery returned 0 endpoints. Diagnostics: orders|metadata|info: Skipped: metadata key 'Swagger:Enabled' not set
```

Common causes:

- `Swagger:Enabled` metadata is missing or not the literal string `"true"`.
- The cluster has no destinations in YARP runtime state (and no static
  destinations either).
- Custom `ConsulYarpConfigProvider` / dynamic destinations require a custom
  `ISwaggerEndpointAddressResolver` (the runtime resolver only knows about
  available + configured destinations on `ClusterState`).

If you want the UI to show *why* it is empty, switch to
`EmptySwaggerEndpointBehavior.DiagnosticEndpoint`; the middleware will serve a
synthetic document at the diagnostics URL whose description carries the
diagnostic list.

### Document name returns 404 even though the cluster exists

The 404 means the discovery service did not produce an endpoint with that
document name. Hit `/swagger/{name}/swagger.json` and look at the JSON
problem body — it lists known document names.

### Document returns 503

The discovery service produced endpoint(s) for the document, but every backend
load failed (HTTP error, timeout, parse error, size limit). The 503 body and
the structured log line both include the failure reason summarized as
`cluster|stage|status`. Partial backend failures do not trigger 503; they are
merged into the document.

## License

MIT — see [LICENSE](LICENSE.md).
