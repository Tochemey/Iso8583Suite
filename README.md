<h2 align="center">
  <img src="assets/logo.svg" alt="Iso8583Suite Logo" width="800"/>
</h2>

<p>
  <a href="https://github.com/Tochemey/Iso8583Suite/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Tochemey/Iso8583Suite/ci.yml" alt="Build Status"/></a>
  <a href="https://codecov.io/gh/Tochemey/Iso8583Suite"><img src="https://codecov.io/gh/Tochemey/Iso8583Suite/branch/main/graph/badge.svg?token=y6tAbZa8VK" alt="codecov"/></a>
  <a href="https://opensource.org/licenses/Apache-2.0"><img src="https://img.shields.io/badge/License-Apache_2.0-blue.svg" alt="License"/></a>
  <a href="https://www.nuget.org/packages/Iso8583.Client"><img src="https://img.shields.io/nuget/v/Iso8583.Client?label=Iso8583.Client" alt="Iso8583.Client NuGet"/></a>
  <a href="https://www.nuget.org/packages/Iso8583.Server"><img src="https://img.shields.io/nuget/v/Iso8583.Server?label=Iso8583.Server" alt="Iso8583.Server NuGet"/></a>
  <a href="https://www.nuget.org/packages/Iso8583.Client"><img src="https://img.shields.io/nuget/dt/Iso8583.Client?label=Iso8583.Client%20downloads" alt="Iso8583.Client Downloads"/></a>
  <a href="https://www.nuget.org/packages/Iso8583.Server"><img src="https://img.shields.io/nuget/dt/Iso8583.Server?label=Iso8583.Server%20downloads" alt="Iso8583.Server Downloads"/></a>
</p>

A high-performance .NET TCP client and server library for [ISO 8583](https://en.wikipedia.org/wiki/ISO_8583) financial messaging, built on [NetCore8583](https://github.com/Tochemey/NetCore8583) and [SpanNetty](https://github.com/cuteant/SpanNetty). ISO 8583 is the standard used by payment networks worldwide to exchange transaction data between point-of-sale terminals, ATMs, acquirers, and card issuers.

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

| Category         | Capability                                                                     |
|------------------|--------------------------------------------------------------------------------|
| Networking       | Async TCP server and client with non-blocking I/O                              |
| Correlation      | Request/response matching via STAN (field 11) with timeout                     |
| Scaling          | Connection pooling with pluggable load balancing (round-robin, least-conn)    |
| Resilience       | Auto-reconnection with exponential backoff and jitter                          |
| Security         | TLS/SSL with mutual TLS support                                                |
| Extensibility    | Composable message handler chain (copy-on-write, lock-free reads)              |
| Observability    | Metrics interface for Prometheus, OpenTelemetry, etc.                          |
| Safety           | Sensitive data masking (PAN, track data) in logs                               |
| Keepalive        | Idle detection with automatic echo keepalive                                   |
| Lifecycle        | Graceful shutdown with configurable drain period, `IAsyncDisposable`           |
| Validation       | Declarative per-field validation, startup configuration validation             |

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

| Method            | Description                                                                             |
|-------------------|-----------------------------------------------------------------------------------------|
| `NewMessage`      | Creates a new ISO message from a raw MTI or enum-based components                       |
| `CreateResponse`  | Creates a response message (MTI = request MTI + `0x0010`), optionally copying fields    |
| `ParseMessage`    | Parses raw bytes into an ISO message                                                    |

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

| Method                  | Description                                                       |
|-------------------------|-------------------------------------------------------------------|
| `Start`                 | Binds to the configured port and begins accepting connections     |
| `Shutdown`              | Gracefully shuts down with a configurable drain period            |
| `AddMessageListener`    | Registers a message handler in the chain                          |
| `RemoveMessageListener` | Removes a previously registered message handler                   |
| `IsStarted`             | Indicates whether the server channel is open and active           |
| `DisposeAsync`          | Disposes the server. Idempotent, suppresses errors                |

### Properties

| Property                | Description                           |
|-------------------------|---------------------------------------|
| `ActiveConnectionCount` | Current number of connected clients   |
| `ActiveConnections`     | All currently active client channels  |

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

| Method                  | Description                                                                   |
|-------------------------|-------------------------------------------------------------------------------|
| `Connect`               | Connects to the server. Supports IP addresses and DNS hostnames               |
| `Send`                  | Fire-and-forget send, with an optional write timeout                          |
| `SendAndReceive`        | Sends a request and waits for the correlated response (STAN + MTI matching)  |
| `Disconnect`            | Gracefully disconnects and cancels all pending requests                       |
| `IsConnected`           | Indicates whether the channel is currently active                             |
| `AddMessageListener`    | Registers a handler for incoming messages (e.g. unsolicited notifications)    |
| `RemoveMessageListener` | Removes a previously registered handler                                       |
| `IsStarted`             | Indicates whether the channel is open                                         |
| `DisposeAsync`          | Disconnects and releases all resources. Idempotent                            |

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

| Method                  | Description                                                              |
|-------------------------|--------------------------------------------------------------------------|
| `Connect`               | Connects all pooled connections to the server in parallel                |
| `Send`                  | Fire-and-forget send on a load-balanced connection                       |
| `SendAndReceive`        | Load-balanced send that waits for the correlated response                |
| `Disconnect`            | Gracefully disconnects all pooled connections and stops health checks    |
| `AddMessageListener`    | Registers a listener on every connection in the pool (and replacements)  |
| `RemoveMessageListener` | Removes a previously registered listener from every connection           |
| `DisposeAsync`          | Disposes all connections and releases pool resources. Idempotent         |

### Properties

| Property                | Description                                               |
|-------------------------|-----------------------------------------------------------|
| `PoolSize`              | Total number of connections in the pool                   |
| `ActiveConnectionCount` | Current number of connections reporting as healthy/active |

### PooledClientConfiguration

| Property              | Type                  | Default | Description                                                                                      |
|-----------------------|-----------------------|---------|--------------------------------------------------------------------------------------------------|
| `PoolSize`            | `int`                 | `4`     | Number of persistent connections to maintain. Must be > 0                                        |
| `HealthCheckInterval` | `TimeSpan`            | `10s`   | How often to check each connection and replace any that have become inactive                     |
| `ClientConfiguration` | `ClientConfiguration` | `new()` | Base configuration applied to every pooled connection (frame encoding, TLS, reconnect, etc.)     |

### Load Balancers

Pass any implementation of `ILoadBalancer` as the third constructor argument. Defaults to `RoundRobinLoadBalancer` when omitted.

| Balancer                       | Selection strategy                                                                                                                        |
|--------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| `RoundRobinLoadBalancer`       | Cycles through active connections atomically via `Interlocked.Increment`. Lock-free, predictable distribution                             |
| `LeastConnectionsLoadBalancer` | Picks the connection with the fewest in-flight requests. Best for workloads with variable response times. Requires a pending-count source |

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

| Aspect                     | Behavior                                                                                                                                                                                               |
|----------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Health checks              | Every `HealthCheckInterval`, the pool scans all connections and replaces any that have become inactive. Recovery is best-effort and retries on the next cycle on failure.                              |
| Listener propagation       | Listeners added via `AddMessageListener` are applied to every connection, including any created during health-check recovery, so you never miss messages on a replaced connection.                    |
| Correlation                | Each pooled connection has its own `PendingRequestManager`, so STAN-based correlation works per connection. `SendAndReceive` picks a connection via the load balancer and waits on that same one.     |
| Empty pool                 | If the load balancer is asked to select from an empty set of active connections, it throws `InvalidOperationException`. Combine with `AutoReconnect = true` for fastest recovery.                      |

## Message Handlers

Implement `IIsoMessageListener<T>` to handle incoming messages. Handlers form a chain: return `false` from `HandleMessage` to stop the chain, `true` to pass the message to the next handler.

### Methods

| Method             | Description                                                                    |
|--------------------|--------------------------------------------------------------------------------|
| `CanHandleMessage` | Returns whether this handler should process the message (e.g. filter by MTI)   |
| `HandleMessage`    | Processes the message. Return `false` to stop the chain, `true` to continue    |

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

| Validator               | Purpose                                                                 |
|-------------------------|-------------------------------------------------------------------------|
| `LengthValidator`       | Enforces fixed, declared, or protocol-max lengths per `IsoType`         |
| `NumericValidator`      | Ensures the value contains only ASCII digits                            |
| `DateValidator`         | Parses the value against the date/time format implied by the `IsoType`  |
| `LuhnValidator`         | Applies the Luhn mod-10 checksum for PAN validation                     |
| `CurrencyCodeValidator` | Checks against the ISO 4217 numeric code set (or a custom allow-list)   |
| `RegexValidator`        | Matches the value against a configured regular expression               |

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

| Direction         | Behavior on failure                                                                                                                                                                                                            |
|-------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Outbound writes   | Completes the write task with a `MessageValidationException` synchronously. The invalid bytes never reach the encoder or the wire, so the caller sees the failure on `Send` / `SendAndReceive`                                |
| Inbound reads     | Fires an exception event on the pipeline and drops the invalid message. When the server is configured with `ReplyOnError = true`, existing error handlers react and may send an administrative error response                 |

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

## Configuration

### Base Configuration (Client and Server)

All properties below are defined on `ConnectorConfiguration` and apply to both `ClientConfiguration` and `ServerConfiguration`.

| Property                    | Type               | Default         | Description                                                                                  |
|-----------------------------|--------------------|-----------------|----------------------------------------------------------------------------------------------|
| `EncodeFrameLengthAsString` | `bool`             | `false`         | `true` = ASCII length header (e.g. `"0152"`), `false` = binary                               |
| `FrameLengthFieldLength`    | `int`              | `2`             | Size of the frame length header in bytes. Valid range: 0–4. Set to 0 to omit the header      |
| `FrameLengthFieldOffset`    | `int`              | `0`             | Byte offset of the length field from the start of the frame                                  |
| `FrameLengthFieldAdjust`    | `int`              | `0`             | Compensation value added to the length field (e.g. if the length includes the header itself) |
| `MaxFrameLength`            | `int`              | `8192`          | Maximum message size in bytes. Messages exceeding this are rejected                          |
| `IdleTimeout`               | `int`              | `30`            | Seconds of read/write inactivity before an echo keepalive is sent. `0` to disable            |
| `WorkerThreadCount`         | `int`              | `CPU * 2`       | SpanNetty I/O event loop threads. Minimum 1                                                  |
| `AddLoggingHandler`         | `bool`             | `false`         | Add `IsoMessageLoggingHandler` to the pipeline for diagnostic message logging                |
| `LogSensitiveData`          | `bool`             | `true`          | When `false`, PAN and track data are masked in logs. **Set to `false` in production**        |
| `LogFieldDescription`       | `bool`             | `true`          | Include ISO field names (e.g. "Primary Account Number") in log output                        |
| `SensitiveDataFields`       | `int[]`            | `[34,35,36,45]` | Field numbers to mask when `LogSensitiveData` is `false`                                     |
| `ReplyOnError`              | `bool`             | `false`         | Send an administrative error response (function code 650) on parse failures                  |
| `AddEchoMessageListener`    | `bool`             | `false`         | Auto-respond to echo requests (0x0800) without writing a handler                             |
| `Ssl`                       | `SslConfiguration` | `null`          | TLS/SSL configuration. `null` or `Enabled = false` means plaintext                           |

### Client Configuration

`ClientConfiguration` extends `ConnectorConfiguration` with reconnection settings.

| Property               | Type   | Default | Description                                                                     |
|------------------------|--------|---------|---------------------------------------------------------------------------------|
| `AutoReconnect`        | `bool` | `true`  | Automatically reconnect when the connection is lost                             |
| `ReconnectInterval`    | `int`  | `100`   | Base delay in milliseconds before the first retry (exponential backoff applied) |
| `MaxReconnectDelay`    | `int`  | `30000` | Maximum backoff delay in milliseconds                                           |
| `MaxReconnectAttempts` | `int`  | `10`    | Maximum retry count. `0` = unlimited                                            |

**Reconnection backoff formula:** `delay = min(ReconnectInterval * 2^attempt, MaxReconnectDelay) + random jitter (0–25%)`

### Server Configuration

`ServerConfiguration` extends `ConnectorConfiguration`.

| Property         | Type  | Default | Description                                                                                                  |
|------------------|-------|---------|--------------------------------------------------------------------------------------------------------------|
| `MaxConnections` | `int` | `100`   | Maximum concurrent client connections. `0` = unlimited. Connections beyond this limit are immediately closed |

### Configuration Validation

Both configurations call `Validate()` at construction time and throw `ArgumentException` for invalid values:

| Rule                                   |
|----------------------------------------|
| `MaxFrameLength` must be > 0           |
| `IdleTimeout` must be >= 0             |
| `WorkerThreadCount` must be >= 1       |
| `FrameLengthFieldLength` must be 0–4   |
| `FrameLengthFieldOffset` must be >= 0  |
| `ReconnectInterval` must be > 0        |
| `MaxReconnectDelay` must be > 0        |
| `MaxReconnectAttempts` must be >= 0    |

## TLS/SSL

TLS is configured via the `SslConfiguration` class, set on the `Ssl` property of either client or server configuration.

### Properties

| Property              | Type     | Default | Description                                                                                                                                 |
|-----------------------|----------|---------|---------------------------------------------------------------------------------------------------------------------------------------------|
| `Enabled`             | `bool`   | `false` | Enable TLS on the connection                                                                                                                |
| `CertificatePath`     | `string` | `null`  | Path to the certificate file. **Server:** PFX/PKCS12 format. **Client (mTLS):** PFX/PKCS12 for the client certificate                       |
| `CertificatePassword` | `string` | `null`  | Password for the certificate file, if encrypted                                                                                             |
| `MutualTls`           | `bool`   | `false` | Require mutual TLS (client certificate authentication). **Server:** demands a client certificate. **Client:** presents a client certificate |
| `CaCertificatePath`   | `string` | `null`  | Path to the CA certificate (PEM) for verifying the remote peer                                                                              |
| `TargetHost`          | `string` | `null`  | Server hostname for certificate validation and SNI (client-side only). Defaults to the connection hostname if not set                       |

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

| Method                  | Called when                                                    |
|-------------------------|----------------------------------------------------------------|
| `MessageSent`           | A message is encoded and written to the wire                   |
| `MessageReceived`       | A message is received and decoded from the wire                |
| `MessageHandled`        | A message handler chain completes successfully                 |
| `MessageError`          | An error occurs during message handling                        |
| `ConnectionEstablished` | A new connection is opened (client connects or server accepts) |
| `ConnectionLost`        | A connection is closed                                         |

### Integration Points

| Component                    | Metrics calls                                                     |
|------------------------------|-------------------------------------------------------------------|
| `IsoMessageEncoder`          | `MessageSent` after encoding each message                         |
| `IsoMessageDecoder`          | `MessageReceived` after decoding each message                     |
| `CompositeIsoMessageHandler` | `MessageHandled` on success, `MessageError` on exception          |
| `ConnectionTracker`          | `ConnectionEstablished` and `ConnectionLost` on channel lifecycle |

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

Both client and server accept an optional pipeline configurator to add custom DotNetty handlers. The configurator is called **after** all built-in handlers are added.

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

| Field | Description  |
|-------|--------------|
| 34    | PAN Extended |
| 35    | Track 2 Data |
| 36    | Track 3 Data |
| 45    | Track 1 Data |

Override `SensitiveDataFields` to customize:

```csharp
var config = new ServerConfiguration
{
    LogSensitiveData = false,
    SensitiveDataFields = [2, 34, 35, 36, 45, 52] // add PAN (2) and PIN block (52)
};
```

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
