# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
