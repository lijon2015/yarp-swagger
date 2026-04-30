# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.0] - 2026-04-30

### Breaking

This is a full rewrite. There is no compatibility shim with v2.x; every public
type, namespace layout, and DI registration has changed. Migration is "rewrite
the integration," not "rename a few APIs."

- **`ISwaggerEndpointProvider` removed.** Replaced by a five-interface
  discovery pipeline:
  - `ISwaggerEndpointDiscoveryService` returns endpoints + diagnostics.
  - `ISwaggerEndpointSource` emits cluster candidates (one per cluster id,
    deduped first-source-wins).
  - `ISwaggerEndpointAddressResolver` chooses the base address; resolvers run
    in registration order and the first to return `Resolved` wins.
  - `ISwaggerMetadataAccessor` reads canonical `Swagger:*` metadata keys.
  - `ISwaggerRefreshTrigger` provides change tokens that drive refreshes.
- **`SwaggerEndpoint.DocumentName` is now required.** The discovery service
  always populates it (cluster id is the fallback).
- **Fake `/swagger/v1/swagger.json` placeholder removed.** When discovery
  returns zero documents the UI gets an empty url list (or a clearly labeled
  diagnostic endpoint, depending on `AggregatedSwaggerUIOptions.EmptyBehavior`).
  Configure with `options.ConfigureAggregatedEndpoints(app.Services, ui => ...)`.
- **New `UseSwaggerAggregationDocuments()` middleware.** Runs in front of
  `UseSwagger()` and owns `200` / `404` / `503` semantics:
  - 200 with the OpenAPI document on success.
  - 404 with a `application/problem+json` body listing known documents on an
    unknown name.
  - 503 (configurable) when a known document fails to aggregate.
- **`SwaggerAggregationBuilder` reshaped.** Old `UseEndpointProvider<T>` is
  gone. New methods: `AddSource`, `ClearDefaultSources`, `AddAddressResolver`,
  `InsertAddressResolver`, `ClearDefaultResolvers`, `AddRefreshTrigger`,
  `AddTransformer`, `Configure`, `ConfigureDocumentEndpoint`,
  `UseDocumentLoader`, `UseDocumentMerger`, `UseDocumentStore`.
- **`ISwaggerDocumentMerger.Merge` signature changed** to
  `Merge(string documentName, IReadOnlyList<SwaggerLoadResult>, SwaggerMergeOptions)`.
  The merge options record `MergeOptions` was renamed to `SwaggerMergeOptions`.
- **`SwaggerDocumentCoordinator` rewritten** against `ISwaggerEndpointDiscoveryService`.
  `GetDocumentNames()` is now `GetDocumentNamesAsync(CancellationToken)`.
  `RefreshAllDocumentsAsync` is now `RefreshAllAsync` and the result record
  carries discovery diagnostics.

### Added

- `SwaggerEndpointDiagnostic` and `SwaggerEndpointDiscoveryResult` so logs and
  custom UIs can explain *why* a cluster was kept, skipped, or failed.
- `YarpRuntimeSwaggerEndpointSource` and `YarpConfigurationSwaggerEndpointSource`
  as the two default sources.
- `YarpRuntimeDestinationAddressResolver` and `YarpConfiguredDestinationAddressResolver`
  as the two default address resolvers.
- `OptionsChangeRefreshTrigger`, `PeriodicRefreshTrigger`, and
  `YarpConfigChangeRefreshTrigger`. The refresh service composes registered
  triggers via `CompositeChangeToken`.
- `EmptySwaggerEndpointBehavior` (`NoEndpoints` default, `DiagnosticEndpoint`
  optional) and `AggregatedSwaggerUIOptions.PrimaryDocumentName` for the
  Swagger UI `urls.primaryName` setting.
- Telemetry tags aligned with the long-term plan: `cluster.id`,
  `document.name`, `destination.address`, `swagger.path`, `http.status_code`,
  `failure.stage`, `failure.reason`, `from.cache`. New
  `swagger.discovery.skipped` counter.

### Removed

- `ISwaggerEndpointProvider`, `ConfigBasedSwaggerEndpointProvider`,
  `HybridSwaggerEndpointProvider`, `YarpStateSwaggerEndpointProvider`,
  `SwaggerEndpointDiscoveryHelper` are all gone.
- The fake-`v1` UI placeholder behavior is gone with no compatibility flag.
- `MergeOptions.IncludeFailedServicesWarning` has moved to
  `SwaggerMergeOptions.IncludeFailedServicesWarning` and now defaults to
  `false`. The merged document description should describe the API; failures
  belong in logs and diagnostics.
- `AggregatedSwaggerUIOptions.UseRelativeUrls` removed. The library always
  emits relative URLs (gateway hosts cannot reliably know their public origin).

### Fixed

- All-backends-failed now produces 5xx instead of 200 with an empty document.
  When every endpoint in a document group fails to load, `SwaggerAggregator`
  throws `SwaggerAggregationFailedException`, the coordinator returns a
  failed resolution, and the middleware emits the configured unavailable
  status (default 503). Partial success is still merged.
- `EmptySwaggerEndpointBehavior.DiagnosticEndpoint` now serves a real OpenAPI
  document at `/{prefix}/{DiagnosticsDocumentName}/swagger.json` (default
  `/swagger/diagnostics/swagger.json`). The synthetic document carries the
  current discovery diagnostics in `info.description` so Swagger UI can
  render it without 404'ing.
- `SwaggerAggregationDocumentEndpointOptions.OpenApiSpecVersion` makes the
  output spec version configurable instead of hard-wired to 3.0. Default is
  still 3.0 for the widest tooling interop; set to 3.1 if downstream services
  emit 3.1 and you need the wire format preserved.

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
