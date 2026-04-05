<h2 align="center">
  <img src="assets/logo.svg" alt="Iso8583Suite Logo" width="800"/>
</h2>

<p>
  <a href="https://github.com/Tochemey/Iso8583Suite/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Tochemey/Iso8583Suite/ci.yml" alt="Build Status"/></a>
  <a href="https://codecov.io/gh/Tochemey/Iso8583Suite"><img src="https://codecov.io/gh/Tochemey/Iso8583Suite/branch/main/graph/badge.svg?token=y6tAbZa8VK" alt="codecov"/></a>
  <a href="https://opensource.org/licenses/Apache-2.0"><img src="https://img.shields.io/badge/License-Apache_2.0-blue.svg" alt="License"/></a>
  <a href="https://www.nuget.org/packages/Iso8583.Server"><img src="https://img.shields.io/nuget/v/Iso8583.Server?label=Iso8583Server" alt="Server"/></a>
   <a href="https://www.nuget.org/packages/Iso8583.Client"><img src="https://img.shields.io/nuget/v/Iso8583.Client?label=Iso8583Client" alt="Client"/></a>
  <a href="https://github.com/Tochemey/Iso8583Suite/releases"><img src="https://img.shields.io/github/v/release/Tochemey/Iso8583Suite" alt="Release"/></a>
</p>

A high-performance .NET TCP client and server library for [ISO 8583](https://en.wikipedia.org/wiki/ISO_8583) financial messaging, built on [NetCore8583](https://github.com/Tochemey/NetCore8583) and [SpanNetty](https://github.com/Azure/SpanNetty). ISO 8583 is the standard used by payment networks worldwide to exchange transaction data between point-of-sale terminals, ATMs, acquirers, and card issuers.

Iso8583Suite handles the low-level networking — framing, TLS, reconnection, idle detection, and request/response correlation — so you can focus on your business logic through a simple message handler interface.

Targets **.NET 8**, **.NET 9**, and **.NET 10**.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Message Factory](#message-factory)
- [Server](#server)
- [Client](#client)
- [Connection Pooling](#connection-pooling)
- [Message Handlers](#message-handlers)
- [Message Validation](#message-validation)
- [Health Checks](#health-checks)
- [Configuration](#configuration)
- [TLS/SSL](#tlsssl)
- [Metrics](#metrics)
- [Pipeline Customization](#pipeline-customization)
- [Logging](#logging)
- [Samples](#samples)
- [Benchmarks](#benchmarks)
- [Building and Testing](#building-and-testing)
- [Contributing](#contributing)

## Features

- **Networking** — async TCP server and client with non-blocking I/O
- **Correlation** — request/response matching via STAN (field 11) with timeout
- **Scaling** — connection pooling with pluggable load balancing (round-robin, least-conn)
- **Resilience** — auto-reconnection with exponential backoff and jitter
- **Security** — TLS/SSL with mutual TLS support
- **Extensibility** — composable message handler chain (copy-on-write, lock-free reads)
- **Observability** — metrics interface for Prometheus, OpenTelemetry, etc.
- **Safety** — sensitive data masking (PAN, track data) in logs
- **Auditability** — structured audit log emitted through `ILogger` for PCI DSS / PSD2 trails
- **Keepalive** — idle detection with automatic echo keepalive
- **Lifecycle** — graceful shutdown with configurable drain period, `IAsyncDisposable`
- **Validation** — declarative per-field validation, startup configuration validation
- **Health Checks** — ASP.NET Core `IHealthCheck` integration for client and server

## Installation

```shell
dotnet add package Iso8583.Client
dotnet add package Iso8583.Server
```

## Quick Start

```csharp
// Shared: build the message factory once at startup
var mfact = ConfigParser.CreateDefault();
ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
mfact.UseBinaryMessages = false;
mfact.Encoding = Encoding.ASCII;
var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

// Server
await using var server = new Iso8583Server<IsoMessage>(9000, messageFactory, logger);
server.AddMessageListener(new AuthorizationHandler(messageFactory));
await server.Start();

// Client
await using var client = new Iso8583Client<IsoMessage>(messageFactory);
await client.Connect("127.0.0.1", 9000);

var request = messageFactory.NewMessage(0x1100);
request.SetField(11, new IsoValue(IsoType.ALPHA, "100001", 6));
var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(10));
```

## Message Factory

`IsoMessageFactory<T>` wraps [NetCore8583](https://github.com/Tochemey/NetCore8583) and is required by both client and server. Create one at startup and share it.

```csharp
var mfact = ConfigParser.CreateDefault();
ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
mfact.UseBinaryMessages = false;
mfact.Encoding = Encoding.ASCII;

var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
```

### Methods

- `NewMessage` — creates a new ISO message from a raw MTI or enum-based components
- `CreateResponse` — creates a response message (MTI = request MTI + `0x0010`), optionally copying fields
- `ParseMessage` — parses raw bytes into an ISO message

### MTI Construction

The Message Type Indicator is composed of four parts:

| Digit | Meaning          | Enum              | Values                                                                                                                                                                                                                  |
|-------|------------------|-------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1st   | ISO version      | `Iso8583Version`  | `V1987` (0x0000), `V1993` (0x1000), `V2003` (0x2000), `NATIONAL` (0x8000), `PRIVATE` (0x9000)                                                                                                                           |
| 2nd   | Message class    | `MessageClass`    | `AUTHORIZATION` (0x0100), `FINANCIAL` (0x0200), `FILE_ACTIONS` (0x0300), `REVERSAL_CHARGEBACK` (0x0400), `RECONCILIATION` (0x0500), `ADMINISTRATIVE` (0x0600), `FEE_COLLECTION` (0x0700), `NETWORK_MANAGEMENT` (0x0800) |
| 3rd   | Message function | `MessageFunction` | `REQUEST` (0x0000), `REQUEST_RESPONSE` (0x0010), `ADVICE` (0x0020), `ADVICE_RESPONSE` (0x0030), `NOTIFICATION` (0x0040), `NOTIFICATION_ACK` (0x0050), `INSTRUCTION` (0x0060), `INSTRUCTION_ACK` (0x0070)                |
| 4th   | Message origin   | `MessageOrigin`   | `ACQUIRER` (0x0000), `ACQUIRER_REPEAT` (0x0001), `ISSUER` (0x0002), `ISSUER_REPEAT` (0x0003), `OTHER` (0x0004), `OTHER_REPEAT` (0x0005)                                                                                 |

```csharp
// These are equivalent:
var msg1 = messageFactory.NewMessage(0x0200);
var msg2 = messageFactory.NewMessage(MessageClass.FINANCIAL, MessageFunction.REQUEST, MessageOrigin.ACQUIRER);
```

## Server

### Methods

- `Start` — binds to the configured port and begins accepting connections
- `Shutdown` — gracefully shuts down with a configurable drain period
- `AddMessageListener` — registers a message handler in the chain
- `RemoveMessageListener` — removes a previously registered message handler
- `IsStarted` — indicates whether the server channel is open and active
- `DisposeAsync` — disposes the server. Idempotent, suppresses errors

### Properties

- `ActiveConnectionCount` — current number of connected clients
- `ActiveConnections` — all currently active client channels

### Usage

```csharp
var config = new ServerConfiguration
{
    EncodeFrameLengthAsString = true,
    FrameLengthFieldLength = 4,
    MaxConnections = 100,
    LogSensitiveData = false
};

await using var server = new Iso8583Server<IsoMessage>(9000, config, messageFactory, logger);
server.AddMessageListener(new AuthorizationHandler(messageFactory));
await server.Start();

Console.WriteLine($"Active clients: {server.ActiveConnectionCount}");

// Graceful shutdown with custom drain
await server.Shutdown(TimeSpan.FromSeconds(30));
```

## Client

### Methods

- `Connect` — connects to the server. Supports IP addresses and DNS hostnames
- `Send` — fire-and-forget send, with an optional write timeout
- `SendAndReceive` — sends a request and waits for the correlated response (STAN + MTI matching)
- `Disconnect` — gracefully disconnects and cancels all pending requests
- `IsConnected` — indicates whether the channel is currently active
- `AddMessageListener` — registers a handler for incoming messages (e.g. unsolicited notifications)
- `RemoveMessageListener` — removes a previously registered handler
- `IsStarted` — indicates whether the channel is open
- `DisposeAsync` — disconnects and releases all resources. Idempotent

### Usage

```csharp
var config = new ClientConfiguration
{
    EncodeFrameLengthAsString = true,
    FrameLengthFieldLength = 4,
    AutoReconnect = true,
    ReconnectInterval = 500,
    MaxReconnectAttempts = 10
};

await using var client = new Iso8583Client<IsoMessage>(config, messageFactory);
await client.Connect("payment-gateway.internal", 9000);

// Request/response with correlation
var request = messageFactory.NewMessage(0x1100);
request.SetField(11, new IsoValue(IsoType.ALPHA, "100001", 6)); // STAN for correlation
request.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
request.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));

var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(10));
var approved = response.GetField(39)?.Value?.ToString() == "000";

// Fire-and-forget
await client.Send(request);

// Fire-and-forget with write timeout
await client.Send(request, timeout: 5000);
```

## Connection Pooling

For high-throughput workloads, `PooledIso8583Client<T>` maintains a pool of persistent connections to a single endpoint and distributes requests across them using a pluggable load-balancing strategy. It exposes the same `Send` / `SendAndReceive` API as `Iso8583Client<T>`, making it a drop-in replacement.

### Methods

- `Connect` — connects all pooled connections to the server in parallel
- `Send` — fire-and-forget send on a load-balanced connection
- `SendAndReceive` — load-balanced send that waits for the correlated response
- `Disconnect` — gracefully disconnects all pooled connections and stops health checks
- `AddMessageListener` — registers a listener on every connection in the pool (and replacements)
- `RemoveMessageListener` — removes a previously registered listener from every connection
- `DisposeAsync` — disposes all connections and releases pool resources. Idempotent

### Properties

- `PoolSize` — total number of connections in the pool
- `ActiveConnectionCount` — current number of connections reporting as healthy/active

### PooledClientConfiguration

| Property              | Default | Description                                                                                  |
|-----------------------|---------|----------------------------------------------------------------------------------------------|
| `PoolSize`            | `4`     | Number of persistent connections to maintain. Must be > 0                                    |
| `HealthCheckInterval` | `10s`   | How often to check each connection and replace any that have become inactive                 |
| `ClientConfiguration` | `new()` | Base configuration applied to every pooled connection (frame encoding, TLS, reconnect, etc.) |

### Load Balancers

Pass any implementation of `ILoadBalancer` as the third constructor argument. Defaults to `RoundRobinLoadBalancer` when omitted.

- `RoundRobinLoadBalancer` — cycles through active connections atomically via `Interlocked.Increment`. Lock-free, predictable distribution
- `LeastConnectionsLoadBalancer` — picks the connection with the fewest in-flight requests. Best for workloads with variable response times. Requires a pending-count source

### Usage

```csharp
var config = new PooledClientConfiguration
{
    PoolSize = 8,
    HealthCheckInterval = TimeSpan.FromSeconds(15),
    ClientConfiguration = new ClientConfiguration
    {
        EncodeFrameLengthAsString = true,
        FrameLengthFieldLength = 4,
        AutoReconnect = true,
        ReconnectInterval = 500
    }
};

// Default round-robin
await using var pool = new PooledIso8583Client<IsoMessage>(config, messageFactory);
await pool.Connect("payment-gateway.internal", 9000);

var request = messageFactory.NewMessage(0x1100);
request.SetField(11, new IsoValue(IsoType.ALPHA, "100001", 6));
request.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
request.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));

var response = await pool.SendAndReceive(request, TimeSpan.FromSeconds(10));
```

Using a least-connections balancer (resolves to the pool via a captured reference so the balancer can query per-connection pending counts):

```csharp
PooledIso8583Client<IsoMessage> pool = null!;
pool = new PooledIso8583Client<IsoMessage>(
    config,
    messageFactory,
    new LeastConnectionsLoadBalancer(idx => pool.GetPendingCount(idx)));

await pool.Connect("payment-gateway.internal", 9000);
```

### Behavior

- **Health checks** — every `HealthCheckInterval`, the pool scans all connections and replaces any that have become inactive. Recovery is best-effort and retries on the next cycle on failure.
- **Listener propagation** — listeners added via `AddMessageListener` are applied to every connection, including any created during health-check recovery, so you never miss messages on a replaced connection.
- **Correlation** — each pooled connection has its own `PendingRequestManager`, so STAN-based correlation works per connection. `SendAndReceive` picks a connection via the load balancer and waits on that same one.
- **Empty pool** — if the load balancer is asked to select from an empty set of active connections, it throws `InvalidOperationException`. Combine with `AutoReconnect = true` for fastest recovery.

## Message Handlers

Implement `IIsoMessageListener<T>` to handle incoming messages. Handlers form a chain: return `false` from `HandleMessage` to stop the chain, `true` to pass the message to the next handler.

### Methods

- `CanHandleMessage` — returns whether this handler should process the message (e.g. filter by MTI)
- `HandleMessage` — processes the message. Return `false` to stop the chain, `true` to continue

```csharp
public class AuthorizationHandler : IIsoMessageListener<IsoMessage>
{
    private readonly IsoMessageFactory<IsoMessage> _factory;
    public AuthorizationHandler(IsoMessageFactory<IsoMessage> factory) => _factory = factory;

    public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x1100;

    public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
    {
        var response = _factory.CreateResponse(request);
        response.CopyFieldsFrom(request, 2, 3, 4, 7, 11, 12, 37, 41, 42, 49);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "123456", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));

        await context.WriteAndFlushAsync(response);
        return false; // stop chain -- message handled
    }
}
```

Multiple handlers can be registered. The handler chain processes them in registration order:

```csharp
server.AddMessageListener(new EchoHandler(messageFactory));          // handles 0x0800
server.AddMessageListener(new AuthorizationHandler(messageFactory)); // handles 0x1100
server.AddMessageListener(new ReversalHandler(messageFactory));      // handles 0x0400
```

## Message Validation

Declarative per-field validation catches malformed messages before they reach the wire. The validation handler is **always installed** in both client and server pipelines; attach a `MessageValidator` via `ConnectorConfiguration.MessageValidator` to activate it. When no validator is configured, the handler is a transparent pass-through.

### Built-in Validators

All built-in validators are `IsoType`-aware — they read the `IsoValue.Type` (and `IsoValue.Length` where applicable) and derive their behavior from the NetCore8583 field definition rather than from hand-wired length or format arguments.

- `LengthValidator` — enforces fixed, declared, or protocol-max lengths per `IsoType`
- `NumericValidator` — ensures the value contains only ASCII digits
- `DateValidator` — parses the value against the date/time format implied by the `IsoType`
- `LuhnValidator` — applies the Luhn mod-10 checksum for PAN validation
- `CurrencyCodeValidator` — checks against the ISO 4217 numeric code set (or a custom allow-list)
- `RegexValidator` — matches the value against a configured regular expression

<details>
<summary><b>Validator details — applicable IsoTypes and rules</b></summary>

**`LengthValidator`** — applies to all IsoTypes.
Value length must match:

- the fixed length implied by the IsoType for `DATE4` / `DATE6` / `DATE10` / `DATE12` / `DATE14` / `DATE_EXP` / `TIME` / `AMOUNT`,
- the declared `IsoValue.Length` for `NUMERIC` / `ALPHA` / `BINARY`,
- the protocol max for `LLVAR` (99) / `LLLVAR` (999) / `LLLLVAR` (9999) and their binary counterparts (`LLBIN` / `LLLBIN` / `LLLLBIN`).

**`NumericValidator`** — applies to `NUMERIC`, `AMOUNT`.
Every character must be an ASCII digit.

**`DateValidator`** — applies to `DATE4`, `DATE6`, `DATE10`, `DATE12`, `DATE14`, `DATE_EXP`, `TIME`.
The value must parse under the format implied by the IsoType (`MMdd`, `yyMMdd`, `MMddHHmmss`, `yyMMddHHmmss`, `yyyyMMddHHmmss`, `yyMM`, `HHmmss`). `DateTime` values are accepted as-is.

**`LuhnValidator`** — applies to `NUMERIC`, `LLVAR`, `LLLVAR`, `LLLLVAR`.
Digits-only Luhn mod-10 checksum for PAN validation.

**`CurrencyCodeValidator`** — applies to `NUMERIC`.
Must be a 3-digit ISO 4217 numeric code. Accepts an optional custom allow-list via the constructor.

**`RegexValidator`** — applies to `NUMERIC`, `ALPHA`, `LLVAR`, `LLLVAR`, `LLLLVAR`.
String representation must match the configured regex.

</details>

### Registering Rules

Use the fluent `ForField(n)` API. Multiple rules per field are supported and run in registration order — all rules execute, so a single `Validate` call collects every failure at once.

```csharp
using Iso8583.Common.Validation;
using Iso8583.Common.Validation.Validators;

var validator = new MessageValidator();
validator
    .ForField(2).Required().AddRule(new LuhnValidator()).AddRule(new LengthValidator())
    .ForField(4).Required().AddRule(new NumericValidator()).AddRule(new LengthValidator())
    .ForField(7).AddRule(new DateValidator())
    .ForField(11).AddRule(new NumericValidator()).AddRule(new LengthValidator())
    .ForField(49).AddRule(new CurrencyCodeValidator());

var config = new ClientConfiguration
{
    // ... other settings ...
    MessageValidator = validator
};
```

Fields marked with `.Required()` produce a failure when absent from the message. Fields with rules but no `.Required()` are skipped when absent.

### Pipeline Integration

Once attached to the configuration, the `MessageValidationHandler` sits between `IdleEventHandler` and the application message handler:

- **Outbound writes** — completes the write task with a `MessageValidationException` synchronously. The invalid bytes never reach the encoder or the wire, so the caller sees the failure on `Send` / `SendAndReceive`.
- **Inbound reads** — fires an exception event on the pipeline and drops the invalid message. When the server is configured with `ReplyOnError = true`, existing error handlers react and may send an administrative error response.

### Programmatic Validation

You can also validate a message ad hoc without going through the pipeline:

```csharp
var report = validator.Validate(message);
if (!report.IsValid)
{
    foreach (var error in report.Errors)
        Console.WriteLine($"Field {error.FieldNumber} ({error.ValidatorName}): {error.ErrorMessage}");
}
```

`ValidationReport.ErrorsForField(int)` returns only the errors for a specific field. `ValidationReport.Valid` is a shared empty report.

### Custom Validators

Implement `IFieldValidator` to plug in your own rule. The validator receives the field number and the `IsoValue`, giving access to both the value and its `IsoType` / declared `Length`:

```csharp
public sealed class TerminalIdValidator : IFieldValidator
{
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
        if (value == null || value.Type != IsoType.ALPHA)
            return ValidationResult.Failure(fieldNumber, "expected ALPHA field", nameof(TerminalIdValidator));

        var str = value.Value?.ToString() ?? string.Empty;
        return str.StartsWith("TRM")
            ? ValidationResult.Success(fieldNumber, nameof(TerminalIdValidator))
            : ValidationResult.Failure(fieldNumber, $"Terminal id '{str}' must start with TRM", nameof(TerminalIdValidator));
    }
}
```

## Health Checks

Iso8583Suite ships first-class [ASP.NET Core health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
for both the client and the server, so readiness and liveness endpoints can report the state of the
ISO 8583 connection alongside the rest of your application.

### States

| Component                     | Status    | When                                                                          |
|-------------------------------|-----------|-------------------------------------------------------------------------------|
| `Iso8583ClientHealthCheck<T>` | Healthy   | The client channel is active and connected                                    |
| `Iso8583ClientHealthCheck<T>` | Degraded  | Auto-reconnect is in progress (channel inactive, at least one retry recorded) |
| `Iso8583ClientHealthCheck<T>` | Unhealthy | The client is disconnected and not attempting to reconnect                    |
| `Iso8583ServerHealthCheck<T>` | Healthy   | The server is listening. The result data includes the active connection count |
| `Iso8583ServerHealthCheck<T>` | Unhealthy | The server is not listening (not started or shut down)                        |

Both checks expose diagnostic details via `HealthCheckResult.Data`:

- **Client:** `connected` (bool), `reconnecting` (bool)
- **Server:** `listening` (bool), `activeConnections` (int)

### Registration

Register the checks with the standard `IHealthChecksBuilder` extension methods. The client and server
instances are resolved from the service provider, so add them as singletons first:

```csharp
using Iso8583.Client.HealthChecks;
using Iso8583.Server.HealthChecks;

services.AddSingleton(_ => new Iso8583Client<IsoMessage>(clientConfig, messageFactory));
services.AddSingleton(_ => new Iso8583Server<IsoMessage>(9000, serverConfig, messageFactory));

services.AddHealthChecks()
    .AddIso8583ClientHealthCheck<IsoMessage>("iso8583-client", tags: new[] { "ready" })
    .AddIso8583ServerHealthCheck<IsoMessage>("iso8583-server", tags: new[] { "ready" });
```

Each extension accepts optional `name`, `failureStatus`, and `tags` arguments. An overload that takes
an explicit instance is also available when the client or server is not registered in DI:

```csharp
var client = new Iso8583Client<IsoMessage>(clientConfig, messageFactory);
services.AddHealthChecks().AddIso8583ClientHealthCheck(client);
```

### Exposing the endpoint

```csharp
var app = builder.Build();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
```

## Configuration

### Base Configuration (Client and Server)

All properties below are defined on `ConnectorConfiguration` and apply to both `ClientConfiguration` and `ServerConfiguration`.

| Property                    | Default         | Description                                                                                  |
|-----------------------------|-----------------|----------------------------------------------------------------------------------------------|
| `EncodeFrameLengthAsString` | `false`         | `true` = ASCII length header (e.g. `"0152"`), `false` = binary                               |
| `FrameLengthFieldLength`    | `2`             | Size of the frame length header in bytes. Valid range: 0–4. Set to 0 to omit the header      |
| `FrameLengthFieldOffset`    | `0`             | Byte offset of the length field from the start of the frame                                  |
| `FrameLengthFieldAdjust`    | `0`             | Compensation value added to the length field (e.g. if the length includes the header itself) |
| `MaxFrameLength`            | `8192`          | Maximum message size in bytes. Messages exceeding this are rejected                          |
| `IdleTimeout`               | `30`            | Seconds of read/write inactivity before an echo keepalive is sent. `0` to disable            |
| `WorkerThreadCount`         | `CPU * 2`       | SpanNetty I/O event loop threads. Minimum 1                                                  |
| `AddLoggingHandler`         | `false`         | Add `IsoMessageLoggingHandler` to the pipeline for diagnostic message logging                |
| `LogLevel`                  | `DEBUG`         | DotNetty log level used by the pipeline logging handler and the server's acceptor handler   |
| `LogSensitiveData`          | `true`          | When `false`, PAN and track data are masked in logs. **Set to `false` in production**        |
| `LogFieldDescription`       | `true`          | Include ISO field names (e.g. "Primary Account Number") in log output                        |
| `SensitiveDataFields`       | `[34,35,36,45]` | Field numbers to mask when `LogSensitiveData` is `false`                                     |
| `ReplyOnError`              | `false`         | Send an administrative error response (function code 650) on parse failures                  |
| `AddEchoMessageListener`    | `false`         | Auto-respond to echo requests (0x0800) without writing a handler                             |
| `Ssl`                       | `null`          | TLS/SSL configuration. `null` or `Enabled = false` means plaintext                           |
| `EnableAuditLog`            | `false`         | Install the structured audit log handler in the pipeline (see [Structured Audit Log](#structured-audit-log)) |
| `AuditLogger`               | `null`          | `ILogger` the audit handler emits events through (conventionally created with category `Iso8583.Audit`) |
| `AuditLogIncludeFields`     | `false`         | When `true`, attach a masked dictionary of every present field to each audit event           |

### Client Configuration

`ClientConfiguration` extends `ConnectorConfiguration` with reconnection settings.

| Property               | Default | Description                                                                     |
|------------------------|---------|---------------------------------------------------------------------------------|
| `AutoReconnect`        | `true`  | Automatically reconnect when the connection is lost                             |
| `ReconnectInterval`    | `100`   | Base delay in milliseconds before the first retry (exponential backoff applied) |
| `MaxReconnectDelay`    | `30000` | Maximum backoff delay in milliseconds                                           |
| `MaxReconnectAttempts` | `10`    | Maximum retry count. `0` = unlimited                                            |

**Reconnection backoff formula:** `delay = min(ReconnectInterval * 2^attempt, MaxReconnectDelay) + random jitter (0–25%)`

### Server Configuration

`ServerConfiguration` extends `ConnectorConfiguration`.

| Property         | Default | Description                                                                                                  |
|------------------|---------|--------------------------------------------------------------------------------------------------------------|
| `MaxConnections` | `100`   | Maximum concurrent client connections. `0` = unlimited. Connections beyond this limit are immediately closed |

### Configuration Validation

Both configurations call `Validate()` at construction time and throw `ArgumentException` for invalid values:

- `MaxFrameLength` must be > 0
- `IdleTimeout` must be >= 0
- `WorkerThreadCount` must be >= 1
- `FrameLengthFieldLength` must be 0–4
- `FrameLengthFieldOffset` must be >= 0
- `ReconnectInterval` must be > 0
- `MaxReconnectDelay` must be > 0
- `MaxReconnectAttempts` must be >= 0

## TLS/SSL

TLS is configured via the `SslConfiguration` class, set on the `Ssl` property of either client or server configuration.

### Properties

| Property              | Default | Description                                                                                                                                 |
|-----------------------|---------|---------------------------------------------------------------------------------------------------------------------------------------------|
| `Enabled`             | `false` | Enable TLS on the connection                                                                                                                |
| `CertificatePath`     | `null`  | Path to the certificate file. **Server:** PFX/PKCS12 format. **Client (mTLS):** PFX/PKCS12 for the client certificate                       |
| `CertificatePassword` | `null`  | Password for the certificate file, if encrypted                                                                                             |
| `MutualTls`           | `false` | Require mutual TLS (client certificate authentication). **Server:** demands a client certificate. **Client:** presents a client certificate |
| `CaCertificatePath`   | `null`  | Path to the CA certificate (PEM) for verifying the remote peer                                                                              |
| `TargetHost`          | `null`  | Server hostname for certificate validation and SNI (client-side only). Defaults to the connection hostname if not set                       |

### Server TLS

```csharp
var config = new ServerConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        CertificatePath = "/certs/server.pfx",
        CertificatePassword = "changeit"
    }
};
```

### Server with Mutual TLS

```csharp
var config = new ServerConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        CertificatePath = "/certs/server.pfx",
        CertificatePassword = "changeit",
        MutualTls = true,
        CaCertificatePath = "/certs/ca.pem" // verify client certificates
    }
};
```

### Client TLS

```csharp
var config = new ClientConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        TargetHost = "payment-gateway.internal" // for certificate validation & SNI
    }
};
```

### Client with Mutual TLS

```csharp
var config = new ClientConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        TargetHost = "payment-gateway.internal",
        MutualTls = true,
        CertificatePath = "/certs/client.pfx",
        CertificatePassword = "changeit"
    }
};
```

## Metrics

Implement `IIso8583Metrics` to integrate with your observability stack (Prometheus, OpenTelemetry, Application Insights, etc.). Pass the implementation to the server constructor. When no metrics provider is supplied, `NullIso8583Metrics` (a no-op singleton) is used automatically.

### Methods

- `MessageSent` — a message is encoded and written to the wire
- `MessageReceived` — a message is received and decoded from the wire
- `MessageHandled` — a message handler chain completes successfully
- `MessageError` — an error occurs during message handling
- `ConnectionEstablished` — a new connection is opened (client connects or server accepts)
- `ConnectionLost` — a connection is closed

### Integration Points

- `IsoMessageEncoder` — `MessageSent` after encoding each message
- `IsoMessageDecoder` — `MessageReceived` after decoding each message
- `CompositeIsoMessageHandler` — `MessageHandled` on success, `MessageError` on exception
- `ConnectionTracker` — `ConnectionEstablished` and `ConnectionLost` on channel lifecycle

### Example Implementation

```csharp
public class PrometheusMetrics : IIso8583Metrics
{
    private static readonly Counter Sent = Metrics.CreateCounter("iso8583_messages_sent_total", "Messages sent", "mti");
    private static readonly Counter Received = Metrics.CreateCounter("iso8583_messages_received_total", "Messages received", "mti");
    private static readonly Histogram Handled = Metrics.CreateHistogram("iso8583_message_duration_seconds", "Handler duration", "mti");
    private static readonly Counter Errors = Metrics.CreateCounter("iso8583_message_errors_total", "Handler errors", "mti");
    private static readonly Gauge Connections = Metrics.CreateGauge("iso8583_active_connections", "Active connections");

    public void MessageSent(int mti) => Sent.WithLabels($"0x{mti:X4}").Inc();
    public void MessageReceived(int mti) => Received.WithLabels($"0x{mti:X4}").Inc();
    public void MessageHandled(int mti, TimeSpan duration) => Handled.WithLabels($"0x{mti:X4}").Observe(duration.TotalSeconds);
    public void MessageError(int mti, Exception ex) => Errors.WithLabels($"0x{mti:X4}").Inc();
    public void ConnectionEstablished() => Connections.Inc();
    public void ConnectionLost() => Connections.Dec();
}

// Pass to server
var server = new Iso8583Server<IsoMessage>(9000, config, messageFactory, logger, metrics: new PrometheusMetrics());
```

## Pipeline Customization

Both client and server accept an optional pipeline configurator to add custom SpanNetty handlers. The configurator is called **after** all built-in handlers are added.

### Built-in Pipeline Order

| Order | Handler                                                                                | Added when                         |
|-------|----------------------------------------------------------------------------------------|------------------------------------|
| 1     | `ConnectionTracker`                                                                    | Server only                        |
| 2     | `TlsHandler`                                                                           | `Ssl.Enabled = true`               |
| 3     | Frame decoder (`StringLengthFieldBasedFrameDecoder` or `LengthFieldBasedFrameDecoder`) | Always                             |
| 4     | `IsoMessageDecoder`                                                                    | Always                             |
| 5     | `IsoMessageEncoder`                                                                    | Always                             |
| 6     | `IsoMessageLoggingHandler`                                                             | `AddLoggingHandler = true`         |
| 7     | `ParseExceptionHandler`                                                                | `ReplyOnError = true`              |
| 8     | `IdleStateHandler` + `IdleEventHandler`                                                | Always                             |
| 9     | `CompositeIsoMessageHandler`                                                           | Always                             |
| 10    | `ReconnectOnCloseHandler`                                                              | Client with `AutoReconnect = true` |
| 11    | Custom configurator                                                                    | When a configurator is provided    |

### Server Pipeline Configurator

Implement `IServerConnectorConfigurator<ServerConfiguration>`:

```csharp
public class MyServerConfigurator : IServerConnectorConfigurator<ServerConfiguration>
{
    public void ConfigureBootstrap(ServerBootstrap bootstrap, ServerConfiguration config)
    {
        bootstrap.ChildOption(ChannelOption.SoSndbuf, 65536);
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ServerConfiguration config)
    {
        pipeline.AddLast("myHandler", new MyCustomHandler());
    }
}

var server = new Iso8583Server<IsoMessage>(9000, config, messageFactory, logger, configurator: new MyServerConfigurator());
```

### Client Pipeline Configurator

Implement `IClientConnectorConfigurator<ClientConfiguration>`:

```csharp
public class MyClientConfigurator : IClientConnectorConfigurator<ClientConfiguration>
{
    public void ConfigureBootstrap(Bootstrap bootstrap, ClientConfiguration config)
    {
        bootstrap.Option(ChannelOption.SoSndbuf, 65536);
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ClientConfiguration config)
    {
        pipeline.AddLast("myHandler", new MyCustomHandler());
    }
}

var client = new Iso8583Client<IsoMessage>(config, messageFactory, configurator: new MyClientConfigurator());
```

## Logging

The library depends on `ILogger` from `Microsoft.Extensions.Logging` — bring your own provider (NLog, Serilog, console, etc.).

### Sensitive Data Masking

When `LogSensitiveData = false`, the logging handler masks fields listed in `SensitiveDataFields`. PAN values are masked as `123456****1234` (first 6, last 4 visible). The default masked fields are:

- Field 34 — PAN Extended
- Field 35 — Track 2 Data
- Field 36 — Track 3 Data
- Field 45 — Track 1 Data

Override `SensitiveDataFields` to customize:

```csharp
var config = new ServerConfiguration
{
    LogSensitiveData = false,
    SensitiveDataFields = [2, 34, 35, 36, 45, 52] // add PAN (2) and PIN block (52)
};
```

### Structured Audit Log

`Iso8583MessageLoggingHandler` emits diagnostic output for humans; for compliance audit trails
(PCI DSS, PSD2, scheme audit) the library also ships a dedicated audit handler that writes one
**structured** event per inbound and outbound message. The handler is published through a
standard `ILogger` so the host application's logging pipeline — Serilog, NLog, OpenTelemetry —
owns format and transport. The library does not own the transport.

Enable it by setting two properties on the configuration:

```csharp
var loggerFactory = LoggerFactory.Create(b => b.AddSerilog()); // or AddNLog, AddOpenTelemetry, …

var serverConfig = new ServerConfiguration
{
    EnableAuditLog = true,
    AuditLogger = loggerFactory.CreateLogger(Iso8583AuditLogHandler.AuditLogCategory), // "Iso8583.Audit"
    AuditLogIncludeFields = false // set true to attach a masked dictionary of every present field
};
```

Each event carries the following structured properties in the logger scope:

| Property                  | Description                                                                                       |
|---------------------------|---------------------------------------------------------------------------------------------------|
| `Iso8583.Direction`       | `Inbound` or `Outbound`                                                                           |
| `Iso8583.Mti`             | Four-digit message type indicator (e.g. `0200`)                                                   |
| `Iso8583.Stan`            | Field 11 — system trace audit number                                                              |
| `Iso8583.Rrn`             | Field 37 — retrieval reference number (when present)                                              |
| `Iso8583.CorrelationId`   | Derived from the MTI class byte and STAN; identical on a request and its matching response      |
| `Iso8583.RemoteEndpoint`  | Remote address of the channel                                                                     |
| `Iso8583.DurationMs`      | On response events only: elapsed milliseconds since the matching request on the same channel     |
| `Iso8583.Fields`          | Optional masked dictionary of every present field; emitted only when `AuditLogIncludeFields=true` |

Field values in `Iso8583.Fields` go through the same `SensitiveDataMasker` used by the diagnostic
logging handler, so PAN (field 2) is reduced to first-six + last-four and fields listed in
`SensitiveDataFields` are replaced with `***`.

#### Serilog → JSON file (ELK / Splunk)

```csharp
var log = new LoggerConfiguration()
    .Filter.ByIncludingOnly(Matching.FromSource("Iso8583.Audit"))
    .WriteTo.File(new JsonFormatter(), "audit.log")
    .CreateLogger();

var loggerFactory = LoggerFactory.Create(b => b.AddSerilog(log));
serverConfig.AuditLogger = loggerFactory.CreateLogger(Iso8583AuditLogHandler.AuditLogCategory);
```

Every audit event is written as a JSON line including the scoped `Iso8583.*` properties, ready
to be shipped to an ELK or Splunk pipeline.

#### OpenTelemetry log exporter

```csharp
var loggerFactory = LoggerFactory.Create(b => b.AddOpenTelemetry(o =>
{
    o.IncludeScopes = true; // required for the Iso8583.* scope properties to flow through
    o.AddOtlpExporter();
}));

serverConfig.AuditLogger = loggerFactory.CreateLogger(Iso8583AuditLogHandler.AuditLogCategory);
```

The same scoped properties are exported to an OTEL collector alongside the log record, where
they can be indexed, filtered, or forwarded to any supported sink.

#### Cost when disabled

When `EnableAuditLog = false` (the default) the handler is **not installed** in the pipeline, so
there is zero per-message overhead.

## Samples

For working end-to-end examples, run the [SampleServer](SampleServer) and [SampleClient](SampleClient) projects:

```bash
# Terminal 1
dotnet run --project SampleServer

# Terminal 2
dotnet run --project SampleClient
```

The client sends an authorization request (0x1100) and the server responds with 0x1110 (approved).

## Benchmarks

```bash
dotnet run --project Iso8583.Benchmarks -c Release
# Run a specific benchmark:
dotnet run --project Iso8583.Benchmarks -c Release -- --filter '*EncoderBenchmarks*'
```

Results on Apple M1, .NET 10:

| Operation                    |     Throughput | Alloc/op |
|------------------------------|---------------:|---------:|
| Create message + 15 fields   |   ~1,820,000/s |  2.29 KB |
| Create response from request |   ~1,126,000/s |  2.27 KB |
| Encode message to wire       |     ~112,000/s |  3.69 KB |
| Request/response correlation |     ~510,000/s |  1.18 KB |
| Frame length parse (4-byte)  | ~143,000,000/s |      0 B |

## Building and Testing

```bash
# Build
dotnet build Iso8583Suite.slnx

# Test
dotnet test Iso8583Suite.slnx
```

149 tests, 90% method coverage.

## Contributing

Contributions are welcome! Please read the [contributing guide](CONTRIBUTING.md) for details on how to get started, submit pull requests, and follow the project's code guidelines.
