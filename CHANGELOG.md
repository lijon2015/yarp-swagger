# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
