# YARP Swagger Aggregation

![dotnet_main](https://github.com/andreytreyt/yarp-swagger/actions/workflows/dotnet.yml/badge.svg?branch=main)
![release](https://github.com/andreytreyt/yarp-swagger/actions/workflows/release.yml/badge.svg)
[![nuget_v](https://img.shields.io/nuget/v/Treyt.Yarp.ReverseProxy.Swagger?logo=nuget)](https://www.nuget.org/packages/Treyt.Yarp.ReverseProxy.Swagger/)
![nuget_dt](https://img.shields.io/nuget/dt/Treyt.Yarp.ReverseProxy.Swagger?logo=nuget)

Swagger aggregation for YARP (Yet Another Reverse Proxy). Automatically discovers and aggregates Swagger documents from backend services using YARP cluster metadata.

## Features

- **Background Refresh** - Asynchronous document loading with configurable refresh intervals
- **YARP Metadata Discovery** - Configure Swagger endpoints via YARP cluster metadata (DRY principle)
- **OAuth2 Support** - Built-in support for client credentials flow via Duende.AccessTokenManagement
- **Resilience** - Polly-based retry, circuit breaker, and timeout policies
- **Telemetry** - OpenTelemetry metrics and distributed tracing
- **Path Filtering** - Regex-based path filtering with ReDoS protection
- **Document Grouping** - Group multiple services into separate Swagger documents

## Getting Started

Configure [Swagger](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle) and [YARP](https://microsoft.github.io/reverse-proxy/articles/getting-started.html) for your project.

### Installation

```bash
dotnet add package Treyt.Yarp.ReverseProxy.Swagger
```

### Configuration

Update `appsettings.json`:

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

Update `Program.cs`:

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

That's it! The Swagger UI will automatically display aggregated endpoints from all configured backend services.

![image](https://raw.githubusercontent.com/andreytreyt/yarp-swagger/main/README.png)

## Cluster Metadata Options

Configure Swagger discovery via YARP cluster metadata:

| Metadata Key                | Description                                         | Default                    |
| --------------------------- | --------------------------------------------------- | -------------------------- |
| `Swagger:Enabled`           | Enable Swagger aggregation for this cluster         | `false`                    |
| `Swagger:Path`              | Path to the Swagger JSON document                   | `/swagger/v1/swagger.json` |
| `Swagger:Prefix`            | Path prefix to add to all operations                | (none)                     |
| `Swagger:PathFilter`        | Regex pattern to filter paths (max 500 chars)       | (none)                     |
| `Swagger:OnlyPublishedPaths`| Only include published paths                        | `false`                    |
| `Swagger:IsMetadataSource`  | Use this cluster's document info as metadata source | `false`                    |
| `Swagger:AccessTokenClient` | Name of the OAuth2 client for authentication        | (none)                     |
| `Swagger:DocumentName`      | Document name for grouping (defaults to ClusterId)  | (cluster id)               |

## Aggregation Options

Configure via `SwaggerAggregation` section in `appsettings.json`:

| Option               | Description                                    | Default                    | Range            |
| -------------------- | ---------------------------------------------- | -------------------------- | ---------------- |
| `RefreshInterval`    | Background refresh interval                    | `00:05:00`                 | 10s - 24h        |
| `LoadTimeout`        | Timeout for loading each Swagger document      | `00:00:30`                 | 5s - 5min        |
| `AggregationTimeout` | Overall aggregation process timeout            | `00:02:00`                 | 30s - 10min      |
| `MaxParallelism`     | Maximum parallel document loads                | `10`                       | 1 - 50           |
| `MaxRetryAttempts`   | Maximum retry attempts for failed loads        | `3`                        | 0 - 10           |
| `DefaultSwaggerPath` | Default Swagger path when metadata unspecified | `/swagger/v1/swagger.json` | max 200 chars    |
| `StartupDelay`       | Initial delay before first refresh             | `00:00:05`                 | -                |
| `MaxDocumentSizeBytes` | Maximum document size to prevent OOM         | `10485760` (10MB)          | 1KB - 100MB      |

## Authentication

For protected Swagger endpoints, configure OAuth2 client credentials:

Update `appsettings.json`:

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

Update `Program.cs`:

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

## Advanced Configuration

### Custom Document Transformers

Register custom transformers to modify Swagger documents:

```csharp
public class MyCustomTransformer : ISwaggerDocumentTransformer
{
    public int Order => 100; // Execution order (lower runs first)

    public ValueTask<OpenApiDocument> TransformAsync(
        OpenApiDocument document,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Modify document here
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

### Custom Endpoint Provider

Replace the default configuration-based endpoint discovery:

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseEndpointProvider<MyCustomEndpointProvider>();
    });
```

### Custom Document Loader

Replace the default HTTP-based document loader:

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentLoader<MyCustomDocumentLoader>();
    });
```

### Custom Document Store

Replace the default in-memory document store (e.g., for distributed caching):

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentStore<RedisDocumentStore>();
    });
```

### Custom Document Merger

Replace the default document merger:

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentMerger<MyCustomMerger>();
    });
```

### Programmatic Configuration

Configure options via code:

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

## Telemetry

The library emits OpenTelemetry metrics and traces under the source name `Yarp.Swagger.Aggregation`.

### Metrics

| Metric                      | Type       | Description                              |
| --------------------------- | ---------- | ---------------------------------------- |
| `swagger.refresh.count`     | Counter    | Number of refresh operations             |
| `swagger.load.success`      | Counter    | Number of successful document loads      |
| `swagger.load.failure`      | Counter    | Number of failed document loads          |
| `swagger.cache.hit`         | Counter    | Number of cache hits                     |
| `swagger.load.duration`     | Histogram  | Load duration in milliseconds            |
| `swagger.refresh.duration`  | Histogram  | Refresh duration in milliseconds         |
| `swagger.endpoints.count`   | Gauge      | Number of discovered endpoints           |

### Traces

- `LoadSwaggerDocument` - Activity for each document load operation
- `AggregateSwaggerDocuments` - Activity for the aggregation process

### Enable Telemetry

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

## Architecture

```text
src/Yarp.ReverseProxy.Swagger/
├── Abstractions/           # Core interfaces (ISwaggerAggregator, ISwaggerDocumentLoader, etc.)
├── Adapters/Swashbuckle/   # Swashbuckle integration (AggregatedSwaggerProvider)
├── Background/             # Background refresh service (SwaggerRefreshService, SwaggerAggregator)
├── Configuration/          # Options and builder (SwaggerAggregationOptions, MetadataKeys)
├── Discovery/              # Endpoint discovery (ConfigBasedSwaggerEndpointProvider)
├── Extensions/             # Service registration (AddSwaggerAggregation)
├── Loading/                # HTTP document loader with resilience
├── Merging/                # Document merger (paths, components, tags)
├── Resilience/             # Polly-based retry, circuit breaker, timeout
├── Storage/                # In-memory document store
├── Telemetry/              # OpenTelemetry metrics and traces
└── Transforming/           # Path prefix and filter transformers
```

### Key Components

| Component                       | Interface                      | Description                                   |
| ------------------------------- | ------------------------------ | --------------------------------------------- |
| `ConfigBasedSwaggerEndpointProvider` | `ISwaggerEndpointProvider` | Discovers endpoints from YARP configuration   |
| `HttpSwaggerDocumentLoader`     | `ISwaggerDocumentLoader`       | Loads Swagger documents via HTTP with OAuth2  |
| `DefaultSwaggerDocumentMerger`  | `ISwaggerDocumentMerger`       | Merges multiple Swagger documents             |
| `SwaggerAggregator`             | `ISwaggerAggregator`           | Orchestrates loading, transforming, merging   |
| `InMemoryAggregatedDocumentStore` | `IAggregatedDocumentStore`   | Thread-safe in-memory document cache          |
| `SwaggerRefreshService`         | `IHostedService`               | Background service for periodic refresh       |
| `AggregatedSwaggerProvider`     | `IAsyncSwaggerProvider`        | Swashbuckle integration with on-demand loading|
| `PathPrefixTransformer`         | `ISwaggerDocumentTransformer`  | Adds path prefix to operations                |
| `PathFilterTransformer`         | `ISwaggerDocumentTransformer`  | Filters paths using regex                     |

### Data Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     Application Startup                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
            ┌─────────────────────────────────────┐
            │   AddSwaggerAggregation()           │
            │   - Register services               │
            │   - Configure HttpClient            │
            │   - Start background service        │
            └─────────────────────────────────────┘
                              │
                              ▼
            ┌─────────────────────────────────────┐
            │   SwaggerRefreshService             │
            │   - Periodic refresh                │
            │   - Config change detection         │
            └─────────────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        ▼                                           ▼
┌───────────────────┐                 ┌───────────────────────┐
│ ISwaggerEndpoint- │                 │ ISwaggerDocumentLoader│
│ Provider          │────────────────▶│ - HTTP requests       │
│ - Discover from   │   endpoints     │ - OAuth2 tokens       │
│   YARP config     │                 │ - Retry/Circuit break │
└───────────────────┘                 └───────────────────────┘
                                                  │
                                                  ▼ documents
                                      ┌───────────────────────┐
                                      │ ISwaggerDocument-     │
                                      │ Transformer[]         │
                                      │ - PathPrefixTransform │
                                      │ - PathFilterTransform │
                                      └───────────────────────┘
                                                  │
                                                  ▼ transformed
                                      ┌───────────────────────┐
                                      │ ISwaggerDocumentMerger│
                                      │ - Merge paths         │
                                      │ - Merge components    │
                                      └───────────────────────┘
                                                  │
                                                  ▼ merged
                                      ┌───────────────────────┐
                                      │ IAggregatedDocument-  │
                                      │ Store                 │
                                      │ - In-memory cache     │
                                      └───────────────────────┘
                                                  │
                                                  ▼
            ┌─────────────────────────────────────┐
            │   GET /swagger/{doc}/swagger.json   │
            │   AggregatedSwaggerProvider         │
            │   - Cache lookup                    │
            │   - On-demand loading               │
            └─────────────────────────────────────┘
```

## License

This project is licensed under the Apache 2.0 License.
