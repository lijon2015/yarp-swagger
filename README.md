# Yuzhu.Yarp.Swagger

[![NuGet](https://img.shields.io/nuget/v/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yuzhu.Yarp.Swagger?logo=nuget)](https://www.nuget.org/packages/Yuzhu.Yarp.Swagger/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[中文文档](README.zh-CN.md) | English

Swagger document aggregation library for YARP (Yet Another Reverse Proxy). Automatically discovers and aggregates Swagger documents from backend services.

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Advanced Configuration](#advanced-configuration)
- [Troubleshooting](#troubleshooting)
- [Architecture](#architecture)

## Features

- **Background Refresh** - Asynchronous document loading with configurable refresh intervals
- **YARP Metadata Discovery** - Configure Swagger endpoints via YARP cluster metadata (DRY principle)
- **OAuth2 Support** - Built-in support for client credentials flow via Duende.AccessTokenManagement
- **Resilience** - Polly-based retry, circuit breaker, and timeout policies
- **Telemetry** - OpenTelemetry metrics and distributed tracing
- **Path Filtering** - Regex-based path filtering
- **Document Grouping** - Group multiple services into separate Swagger documents

## Prerequisites

Before using this library, ensure you have:

| Requirement | Version |
|-------------|---------|
| .NET | 10.0 or higher |
| YARP | 2.3.0 or higher |
| Swashbuckle.AspNetCore | 10.1.0 or higher |

**Background knowledge you'll need:**

1. **YARP (Yet Another Reverse Proxy)** - Microsoft's reverse proxy library
   - Official docs: <https://microsoft.github.io/reverse-proxy/>
   - Getting started: <https://microsoft.github.io/reverse-proxy/articles/getting-started.html>

2. **Swagger/OpenAPI** - API documentation specification
   - ASP.NET Core integration: <https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle>

## Quick Start

### Step 1: Install NuGet Package

```bash
dotnet add package Yuzhu.Yarp.Swagger
```

### Step 2: Configure appsettings.json

Here's a minimal configuration example (single backend service):

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

### Step 3: Configure Program.cs

```csharp
using Yuzhu.Yarp.Swagger.Adapters.Swashbuckle;
using Yuzhu.Yarp.Swagger.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Add YARP reverse proxy and Swagger aggregation
var configuration = builder.Configuration.GetSection("ReverseProxy");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation();  // Add this line to enable Swagger aggregation

var app = builder.Build();

// 3. Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.ConfigureAggregatedEndpoints(app.Services);  // Configure aggregated endpoints
});

app.MapReverseProxy();

app.Run();
```

### Step 4: Run and Verify

1. Start your backend service (ensure it has Swagger documentation)
2. Start the YARP gateway
3. Visit `https://localhost:<port>/swagger` to view the aggregated Swagger UI

![Swagger UI Screenshot](https://raw.githubusercontent.com/andreytreyt/yarp-swagger/main/README.png)

## Configuration

### Cluster Metadata Options

Configure Swagger discovery via YARP cluster metadata:

| Metadata Key | Description | Default |
|--------------|-------------|---------|
| `Swagger:Enabled` | Enable Swagger aggregation for this cluster | `false` |
| `Swagger:Path` | Path to Swagger JSON document | `/swagger/v1/swagger.json` |
| `Swagger:Prefix` | Path prefix to add to all operations | (none) |
| `Swagger:PathFilter` | Regex pattern to filter paths (max 500 chars) | (none) |
| `Swagger:OnlyPublishedPaths` | Only include published paths | `false` |
| `Swagger:IsMetadataSource` | Use this cluster's document info as metadata source | `false` |
| `Swagger:AccessTokenClient` | OAuth2 client name for authentication | (none) |
| `Swagger:DocumentName` | Document group name (defaults to ClusterId) | (cluster id) |

### Aggregation Options

Configure via `SwaggerAggregation` section in `appsettings.json`:

```json
{
  "SwaggerAggregation": {
    "RefreshInterval": "00:05:00",
    "LoadTimeout": "00:00:30",
    "MaxRetryAttempts": 3
  }
}
```

| Option | Description | Default | Range |
|--------|-------------|---------|-------|
| `RefreshInterval` | Background refresh interval | `00:05:00` | 10s - 24h |
| `LoadTimeout` | Timeout for loading each Swagger document | `00:00:30` | 5s - 5min |
| `AggregationTimeout` | Overall aggregation process timeout | `00:02:00` | 30s - 10min |
| `MaxParallelism` | Maximum parallel document loads | `10` | 1 - 50 |
| `MaxRetryAttempts` | Maximum retry attempts for failed loads | `3` | 0 - 10 |
| `DefaultSwaggerPath` | Default Swagger path when metadata unspecified | `/swagger/v1/swagger.json` | max 200 chars |
| `StartupDelay` | Initial delay before first refresh | `00:00:05` | - |
| `MaxDocumentSizeBytes` | Maximum document size to prevent OOM | `10485760` (10MB) | 1KB - 100MB |
| `MergeIntoSingleDocument` | Merge all documents into one | `false` | - |
| `DocumentName` | Name for merged document | - | - |
| `SchemaConflictStrategy` | Schema conflict resolution strategy | `FirstWins` | - |

## Multi-Service Configuration

Here's an example of aggregating multiple backend services:

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
          "Swagger:Prefix": "/proxy-app1"
        }
      },
      "App2Cluster": {
        "Destinations": {
          "Default": { "Address": "https://localhost:5102" }
        },
        "Metadata": {
          "Swagger:Enabled": "true",
          "Swagger:Prefix": "/proxy-app2"
        }
      }
    }
  }
}
```

## Authentication

For protected Swagger endpoints, configure OAuth2 client credentials:

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

Replace the default in-memory store (e.g., for distributed caching):

```csharp
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(configuration)
    .AddSwaggerAggregation(builder =>
    {
        builder.UseDocumentStore<RedisDocumentStore>();
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

| Metric | Type | Description |
|--------|------|-------------|
| `swagger.refresh.count` | Counter | Number of refresh operations |
| `swagger.load.success` | Counter | Number of successful document loads |
| `swagger.load.failure` | Counter | Number of failed document loads |
| `swagger.cache.hit` | Counter | Number of cache hits |
| `swagger.load.duration` | Histogram | Load duration in milliseconds |
| `swagger.refresh.duration` | Histogram | Refresh duration in milliseconds |
| `swagger.endpoints.count` | Gauge | Number of discovered endpoints |

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

## Troubleshooting

### Common Issues

#### 1. Swagger UI doesn't show aggregated endpoints

**Possible causes:**

- Backend service is not running or not accessible
- `Swagger:Enabled` is not set to `true`
- Swagger path configuration is incorrect

**Solutions:**

1. Confirm the backend service is running
2. Check if the backend's Swagger document is directly accessible (e.g., `https://localhost:5001/swagger/v1/swagger.json`)
3. Ensure `Swagger:Enabled` is set to `"true"` (note: it's a string)

#### 2. Load timeout errors

**Possible causes:**

- Backend service responds slowly
- Network issues
- `LoadTimeout` is set too short

**Solution:**

```json
{
  "SwaggerAggregation": {
    "LoadTimeout": "00:01:00"
  }
}
```

#### 3. Authentication failures

**Possible causes:**

- OAuth2 client configuration is incorrect
- Token endpoint is not accessible
- Invalid client credentials

**Solutions:**

1. Verify Identity Server is running
2. Check `ClientId` and `ClientSecret` are correct
3. Confirm Token endpoint URL is correct

#### 4. Incorrect path prefix

**Possible cause:**

- `Swagger:Prefix` doesn't match YARP route configuration

**Solution:**

Ensure `Swagger:Prefix` matches the `Match.Path` prefix in YARP Route:

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

### Debugging Tips

Enable detailed logging to troubleshoot issues:

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

## Architecture

```text
src/Yuzhu.Yarp.Swagger/
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

### Data Flow

```text
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

This project is licensed under the MIT License.
