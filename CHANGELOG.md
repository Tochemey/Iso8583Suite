# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Connection pooling**: `PooledIso8583Client<T>` maintains a configurable pool of persistent connections to a single endpoint, exposing the same `Send` / `SendAndReceive` API as `Iso8583Client<T>` as a drop-in replacement
- **Load balancing**: pluggable `ILoadBalancer` abstraction with built-in `RoundRobinLoadBalancer` (lock-free via `Interlocked.Increment`) and `LeastConnectionsLoadBalancer` (selects the connection with fewest in-flight requests)
- **PooledClientConfiguration** exposing `PoolSize`, `HealthCheckInterval`, and the underlying `ClientConfiguration`
- **Pool health checks**: periodic scanning replaces inactive connections with fresh `Iso8583Client<T>` instances that reconnect to the same endpoint, with best-effort recovery retried on the next cycle
- **Listener propagation**: pool-level `AddMessageListener` / `RemoveMessageListener` applied to every connection, including those created during health-check recovery
- **Message validation framework**: declarative per-field validation via `MessageValidator` with a fluent `ForField(n).Required().AddRule(...)` API; all configured rules run per `Validate` call so a single invocation surfaces every failure at once
- **Built-in `IsoType`-aware validators**: `LengthValidator`, `NumericValidator`, `DateValidator` (covers DATE4/DATE6/DATE10/DATE12/DATE14/DATE_EXP/TIME), `LuhnValidator` (PAN checksum), `CurrencyCodeValidator` (ISO 4217 with optional allow-list), and `RegexValidator`
- **`MessageValidationHandler`**: unconditionally installed in both client and server pipelines, acting as a transparent pass-through when no validator is configured; outbound writes fail the write task synchronously with `MessageValidationException`, inbound reads fire a pipeline exception event
- **`ValidationReport`** with per-field filtering via `ErrorsForField`, a shared `Valid` singleton, and a diagnostic `ToString()`
- **Custom validators**: implement `IFieldValidator` to plug in project-specific rules with full access to `IsoValue.Type` and declared length
- **`CompositeIsoMessageHandler`** marked `IsSharable = true` to support multi-connection scenarios required by the pooled client
- **Health checks**: ASP.NET Core `IHealthCheck` integration for both client and server, reporting connection state and active connection counts
- **Structured audit log**: opt-in `Iso8583AuditLogHandler` emits one structured event per inbound and outbound message through a caller-supplied `ILogger` under the conventional `Iso8583.Audit` category, exposing scoped properties (`Iso8583.Direction`, `Iso8583.Mti`, `Iso8583.Stan`, `Iso8583.Rrn`, `Iso8583.CorrelationId`, `Iso8583.RemoteEndpoint`, `Iso8583.DurationMs`, optional `Iso8583.Fields`) for downstream routing to Serilog / NLog / OpenTelemetry sinks
- **Per-channel request/response correlation** in the audit handler attaches `Iso8583.DurationMs` to the response event for the matching STAN
- **`SensitiveDataMasker`** shared helper centralises PAN and track-data masking for both the diagnostic logging handler and the audit handler
- **`ConnectorConfiguration.EnableAuditLog` / `AuditLogger` / `AuditLogIncludeFields`** configuration properties to opt into the audit handler and control whether a masked field dictionary is emitted
- **`ConnectorConfiguration.LogLevel`** configuration property drives both the DotNetty server acceptor `LoggingHandler` and the pipeline `IsoMessageLoggingHandler`, replacing the previously hardcoded `INFO` / `DEBUG` levels

### Changed

- `Iso8583Server` and `IsoMessageLoggingHandler` now honor `ConnectorConfiguration.LogLevel` instead of using hardcoded log levels
- `SampleServer` and `SampleClient` set `LogLevel` explicitly and enable the audit logger to demonstrate end-to-end usage

## [0.1.0] - 2026-04-03

### Added

- **Iso8583.Client**: async TCP client with request/response correlation via STAN-based matching
- **Iso8583.Server**: async TCP server with composable message handler chain (lock-free, copy-on-write)
- **Iso8583.Common**: shared infrastructure including message factory, codecs, and pipeline components
- Auto-reconnection with exponential backoff and jitter (client)
- TLS/SSL support including mutual TLS (mTLS)
- Connection tracking and max connection enforcement (server)
- Graceful shutdown with configurable drain period and `IAsyncDisposable` support
- Idle detection with automatic echo keepalive
- Metrics interface with support for Prometheus, OpenTelemetry, and Application Insights
- Sensitive data masking in logs (PAN, track data)
- Configuration validation at startup with clear error messages
- Pipeline customization via `IConnectorConfigurator`
- Frame length encoding (binary or ASCII) with configurable field size
- Sample client and server implementations
- Comprehensive test suite (~90% method coverage)
- Performance benchmarks for message creation, encoding, correlation, and frame parsing
- GitHub Actions CI/CD with build, test, coverage, CodeQL, and NuGet publishing
- Support for .NET 8, 9, and 10
