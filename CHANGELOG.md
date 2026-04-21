# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.1] - 2026-04-21

### Changed

- Code-style cleanup across `src/`, `tests/`, and `sample/` to satisfy the
  repository's `.editorconfig` IDE rules: explicit types in place of `var`,
  expression-bodied members for single-line methods, primary constructors,
  collection expressions (`[...]`), and discard (`_ = ...`) for unused
  expression values. No runtime or public API changes.

### Fixed

- `SwaggerDocumentCoordinatorTests.GetDocumentNames_WhenCacheHasEntries_ReturnsCachedNames`
  is now `async Task` and awaits the store writes directly, replacing
  `.AsTask().GetAwaiter().GetResult()` (xUnit1031).

## [2.0.0] - 2026-04-21

### Breaking

- **`IAggregatedDocumentStore` synchronous methods removed.** `Get(string)` and
  `Exists(string)` have been deleted. Use `GetAsync(string, CancellationToken)`
  and check the returned document for `null`. Any custom implementations must
  drop these members.
- **`Swagger:OnlyPublishedPaths` metadata key removed**, along with
  `SwaggerEndpoint.OnlyPublishedPaths` and `MetadataKeys.OnlyPublishedPaths`.
  The flag was never honored by the transform pipeline. If you need to filter
  unpublished routes, configure `Swagger:PathFilter` with an explicit regex.
- **`SwaggerEndpoint.HttpClientName` (internal) removed.** The single named
  client `SwaggerConstants.HttpClientName` is now used for every endpoint.
- **`SwaggerAggregationOptions` properties changed from `init` to `set`.**
  Record-style `with { ... }` copies on this type now mutate the original
  instance's configuration slot at runtime instead of producing a new options
  record. Migrate to `services.Configure<SwaggerAggregationOptions>(...)` or
  `PostConfigure(...)` for overrides.
- **Default `ISwaggerEndpointProvider` implementation switched to
  `HybridSwaggerEndpointProvider`.** It reads from both `IProxyConfigProvider`
  snapshots and the live YARP runtime state, de-duplicating by `ClusterId`.
  If you were relying on `ConfigBasedSwaggerEndpointProvider` being the
  registered provider (e.g., to ignore runtime-added clusters), register it
  explicitly after calling `AddSwaggerAggregation`.
- **`SwaggerAggregationBuilder.Configure` and the
  `AddSwaggerAggregation(configureOptions, ...)` overload now use
  `PostConfigure`** instead of `Configure`, so user overrides run *after*
  `BindConfiguration`. Callers that deliberately relied on the earlier
  ordering (config file overriding code) must invert their setup.

### Added

- **`SwaggerDocumentCoordinator`** centralizes load/merge orchestration with
  per-document deduplication so concurrent refresh triggers no longer issue
  duplicate backend calls.
- **`HybridSwaggerEndpointProvider`** merges config-based and YARP runtime
  endpoints; it is the new default registration.
- **`SwaggerEndpointDiscoveryHelper`** shared parsing utility for cluster
  metadata → `SwaggerEndpoint` conversion.
- **`SwaggerDocumentUnavailableException`** surfaced when a requested document
  name has no cached result and cannot be produced.
- **`Yuzhu.Yarp.Swagger.Tests`** project with coverage for
  `AggregatedSwaggerProvider` and `HybridSwaggerEndpointProvider`.
- **`HttpClient` `MaxConnectionsPerServer` now tracks `MaxParallelism`**
  instead of the previous hard-coded `10`.

### Changed

- **`SwaggerRefreshService` rewritten** around the new coordinator; the
  refresh loop is simpler, cancellation is respected at every await point,
  and startup-delay handling is consolidated.
- **`AggregatedSwaggerProvider` simplified** (-152 lines). The async path is
  the primary implementation; the sync `ISwaggerProvider` shim delegates to
  it synchronously only because Swashbuckle's contract still requires it.
- XML doc comments on public abstractions translated to English to match the
  rest of the public surface.

### Migration

```csharp
// Before
if (store.Exists(name))
{
    var doc = store.Get(name);
    // ...
}

// After
var doc = await store.GetAsync(name, cancellationToken);
if (doc is not null)
{
    // ...
}
```

```jsonc
// Before — remove this key; it was never applied.
"Swagger:OnlyPublishedPaths": "true",

// After — filter with an explicit regex if needed.
"Swagger:PathFilter": "^/api/public/.*"
```

If you need the old config-only discovery behavior, register the provider
explicitly after `AddSwaggerAggregation`:

```csharp
builder.Services.AddSingleton<ISwaggerEndpointProvider, ConfigBasedSwaggerEndpointProvider>();
```

## [1.1.0] - 2026-04-19

### Fixed

- **Resilience pipeline now actually applied to the Swagger HttpClient.** The
  `AddSwaggerResilienceHandler` extension existed but was never invoked by
  `AddSwaggerAggregation`, so configured `MaxRetryAttempts`, the timeout policy,
  and the circuit breaker were silently inactive. They are now wired in.
- **Removed conflicting HttpClient timeouts.** Previously the named HttpClient
  was hard-coded to a 30 s timeout while `LoadTimeout` was applied via a linked
  CTS in the loader; whichever value was shorter would win unpredictably.
  HttpClient timeout is now `Timeout.InfiniteTimeSpan`, the resilience
  pipeline enforces per-attempt `LoadTimeout`, and the loader keeps an outer
  budget of `LoadTimeout × (MaxRetryAttempts + 1) + 5 s` as a backstop.
- **Pre-validate `Content-Length` before downloading.** When the server reports
  a `Content-Length` that exceeds `MaxDocumentSizeBytes`, the load is rejected
  immediately instead of streaming up to the limit and then aborting.
- **Catch `Polly.Timeout.TimeoutRejectedException`** raised by the resilience
  pipeline and report it as a timeout failure with proper telemetry.

### Changed

- **`SwaggerHttpClientExtensions.AddSwaggerResilienceHandler` signature changed.**
  - Before: `void AddSwaggerResilienceHandler(this IHttpClientBuilder, SwaggerAggregationOptions)`
  - After: `IHttpClientBuilder AddSwaggerResilienceHandler(this IHttpClientBuilder)`
  - Options are now resolved from DI at request time via `IOptionsMonitor`,
    so live-reloaded configuration takes effect for new pipelines.
  - The previous overload was unreferenced internally, so most consumers will
    not be affected; if you called it directly you must update your call site.
- **Switched `SwaggerRefreshService` lock object to `System.Threading.Lock`**
  (.NET 9+ optimized lock type).
- **Naming and code-style enforcement.** Removed the catch-all `NoWarn`
  suppression from the project file and aligned `.editorconfig` naming rules
  with standard .NET conventions (`PascalCase` for constants and
  static-readonly fields, `_camelCase` for private instance fields). Builds now
  surface IDE style violations as errors.

### Migration

If you depended on the 30 s hard-coded HttpClient timeout, set `LoadTimeout`
to `00:00:30` (or your desired value) explicitly. With retries and circuit
breaker now active, total time-to-failure can extend up to
`LoadTimeout × (MaxRetryAttempts + 1) + 5 s` — tune `MaxRetryAttempts` if
you need a tighter overall budget.

## [1.0.2] - earlier

See git history.
