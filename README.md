# Iso8583Suite

[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/Tochemey/Iso8583Suite/ci.yml)](https://github.com/Tochemey/Iso8583Suite/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/Tochemey/Iso8583Suite/branch/main/graph/badge.svg?token=y6tAbZa8VK)](https://codecov.io/gh/Tochemey/Iso8583Suite)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Iso8583.Common NuGet](https://img.shields.io/nuget/v/Iso8583.Common?label=Iso8583.Common)](https://www.nuget.org/packages/Iso8583.Common)
[![Iso8583.Client NuGet](https://img.shields.io/nuget/v/Iso8583.Client?label=Iso8583.Client)](https://www.nuget.org/packages/Iso8583.Client)
[![Iso8583.Server NuGet](https://img.shields.io/nuget/v/Iso8583.Server?label=Iso8583.Server)](https://www.nuget.org/packages/Iso8583.Server)

A high-performance .NET TCP client and server library for [ISO 8583](https://en.wikipedia.org/wiki/ISO_8583) financial messaging, built on [NetCore8583](https://github.com/Tochemey/NetCore8583) and [SpanNetty](https://github.com/cuteant/SpanNetty). ISO 8583 is the standard used by payment networks worldwide to exchange transaction data between point-of-sale terminals, ATMs, acquirers, and card issuers.

Iso8583Suite handles the low-level networking -- framing, TLS, reconnection, idle detection, and request/response correlation -- so you can focus on your business logic through a simple message handler interface.

Targets **.NET 8**, **.NET 9**, and **.NET 10**.

## Installation

Install the packages via NuGet:

```shell
dotnet add package Iso8583.Client --version 0.1.0
dotnet add package Iso8583.Server --version 0.1.0
```

## Features

- Async TCP server and client with non-blocking I/O
- Request/response correlation via `SendAndReceive` (STAN-based matching with timeout)
- Auto-reconnection with exponential backoff and jitter
- TLS/SSL with mutual TLS support
- Composable message handler chain (copy-on-write, lock-free reads)
- Connection tracking and max connection enforcement
- Metrics interface for Prometheus, OpenTelemetry, etc.
- Sensitive data masking (PAN, track data) in logs
- Idle detection with automatic echo keepalive
- Graceful shutdown with configurable drain period
- `IAsyncDisposable` on both client and server
- Configuration validation at startup

## Samples

For working end-to-end examples, run the [SampleServer](SampleServer) and [SampleClient](SampleClient) projects:

```bash
# Terminal 1
dotnet run --project SampleServer

# Terminal 2
dotnet run --project SampleClient
```

The client sends an authorization request (0x1100) and the server responds with 0x1110 (approved).

## Message Factory

`IsoMessageFactory<T>` wraps [NetCore8583](https://github.com/Tochemey/NetCore8583) and is required by both client and server. Create one at startup and share it.

```csharp
var mfact = ConfigParser.CreateDefault();
ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
mfact.UseBinaryMessages = false;
mfact.Encoding = Encoding.ASCII;

var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
```

### IMessageFactory&lt;T&gt; Methods

| Method                                                                | Returns | Description                                                                |
|-----------------------------------------------------------------------|---------|----------------------------------------------------------------------------|
| `NewMessage(int type)`                                                | `T`     | Create a message with a raw MTI value (e.g. `0x1100`)                      |
| `NewMessage(MessageClass, MessageFunction, MessageOrigin)`            | `T`     | Create a message with enum-based MTI construction                          |
| `CreateResponse(T request)`                                           | `T`     | Create a response (MTI = request MTI + `0x0010`). Does **not** copy fields |
| `CreateResponse(T request, bool copyAllFields)`                       | `T`     | Create a response, optionally copying all fields from the request          |
| `ParseMessage(byte[] buf, int isoHeaderLength, bool binaryIsoHeader)` | `T`     | Parse raw bytes into an ISO message. Throws `ParseException` on failure    |

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

### Iso8583Server&lt;T&gt; Methods

| Method                                          | Returns     | Description                                                             |
|-------------------------------------------------|-------------|-------------------------------------------------------------------------|
| `Start()`                                       | `Task`      | Bind to the configured port and begin accepting connections             |
| `Shutdown()`                                    | `Task`      | Graceful shutdown with a default 15-second drain period                 |
| `Shutdown(TimeSpan gracePeriod)`                | `Task`      | Graceful shutdown with a custom drain period for in-flight requests     |
| `AddMessageListener(IIsoMessageListener<T>)`    | `void`      | Register a message handler. Listeners are invoked in registration order |
| `RemoveMessageListener(IIsoMessageListener<T>)` | `void`      | Remove a previously registered message handler                          |
| `IsStarted()`                                   | `bool`      | `true` if the server channel is open and active                         |
| `DisposeAsync()`                                | `ValueTask` | Shuts down with a 5-second grace period. Idempotent, suppresses errors  |

### Iso8583Server&lt;T&gt; Properties

| Property                | Type                            | Description                          |
|-------------------------|---------------------------------|--------------------------------------|
| `ActiveConnectionCount` | `int`                           | Current number of connected clients  |
| `ActiveConnections`     | `IReadOnlyCollection<IChannel>` | All currently active client channels |

### Iso8583Server&lt;T&gt; Constructors

| Constructor                                                                                                                                                                                                     | Description                                                                   |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------|
| `Iso8583Server(int port, ServerConfiguration config, IMessageFactory<T> factory, ILogger logger = null, IServerConnectorConfigurator<ServerConfiguration> configurator = null, IIso8583Metrics metrics = null)` | Full constructor with all options                                             |
| `Iso8583Server(int port, IMessageFactory<T> factory, ILogger logger = null)`                                                                                                                                    | Minimal constructor using default `ServerConfiguration` (max 100 connections) |

### Server Usage

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

// Inspect connections
Console.WriteLine($"Active clients: {server.ActiveConnectionCount}");

// Graceful shutdown with custom drain
await server.Shutdown(TimeSpan.FromSeconds(30));
```

## Client

### Iso8583Client&lt;T&gt; Methods

| Method                                                                       | Returns            | Description                                                                                                                                                                                               |
|------------------------------------------------------------------------------|--------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Connect(string host, int port)`                                             | `Task`             | Connect to the server. Supports IP addresses and DNS hostnames. Resets the reconnect counter on success                                                                                                   |
| `Send(IsoMessage message)`                                                   | `Task`             | Fire-and-forget send. Throws `InvalidOperationException` if not connected                                                                                                                                 |
| `Send(IsoMessage message, int timeout)`                                      | `Task`             | Send with a write timeout in milliseconds. Throws `TimeoutException` if the write does not complete in time                                                                                               |
| `SendAndReceive(IsoMessage message, TimeSpan timeout, CancellationToken ct)` | `Task<IsoMessage>` | Send a request and wait for the correlated response. Correlation uses field 11 (STAN) and message type (response MTI = request MTI + `0x0010`). Throws `TimeoutException` or `OperationCanceledException` |
| `Disconnect()`                                                               | `Task`             | Gracefully disconnect. Cancels all pending requests and shuts down the event loop                                                                                                                         |
| `IsConnected()`                                                              | `bool`             | `true` if the channel is active                                                                                                                                                                           |
| `AddMessageListener(IIsoMessageListener<T>)`                                 | `void`             | Register a handler for incoming messages (e.g. unsolicited server notifications)                                                                                                                          |
| `RemoveMessageListener(IIsoMessageListener<T>)`                              | `void`             | Remove a previously registered handler                                                                                                                                                                    |
| `IsStarted()`                                                                | `bool`             | `true` if the channel is open                                                                                                                                                                             |
| `DisposeAsync()`                                                             | `ValueTask`        | Disconnect and release all resources. Idempotent                                                                                                                                                          |

### Iso8583Client&lt;T&gt; Constructors

| Constructor                                                                                                                                    | Description                                                          |
|------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| `Iso8583Client(ClientConfiguration config, IMessageFactory<T> factory, IClientConnectorConfigurator<ClientConfiguration> configurator = null)` | Full constructor with configuration and optional pipeline customizer |
| `Iso8583Client(IMessageFactory<T> factory)`                                                                                                    | Minimal constructor using default `ClientConfiguration`              |

### Client Usage

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

## Message Handlers

Implement `IIsoMessageListener<T>` to handle incoming messages. Handlers form a chain: return `false` from `HandleMessage` to stop the chain, `true` to pass the message to the next handler.

| Method                                                 | Returns      | Description                                                                   |
|--------------------------------------------------------|--------------|-------------------------------------------------------------------------------|
| `CanHandleMessage(T message)`                          | `bool`       | Return `true` if this handler should process the message (e.g. filter by MTI) |
| `HandleMessage(IChannelHandlerContext ctx, T message)` | `Task<bool>` | Process the message. Return `false` to stop the chain, `true` to continue     |

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
server.AddMessageListener(new EchoHandler(messageFactory));       // handles 0x0800
server.AddMessageListener(new AuthorizationHandler(messageFactory)); // handles 0x1100
server.AddMessageListener(new ReversalHandler(messageFactory));     // handles 0x0400
```

## Configuration

### Base Configuration (Client and Server)

All properties below are defined on `ConnectorConfiguration` and apply to both `ClientConfiguration` and `ServerConfiguration`.

| Property                    | Type               | Default         | Description                                                                                  |
|-----------------------------|--------------------|-----------------|----------------------------------------------------------------------------------------------|
| `EncodeFrameLengthAsString` | `bool`             | `false`         | `true` = ASCII length header (e.g. `"0152"`), `false` = binary                               |
| `FrameLengthFieldLength`    | `int`              | `2`             | Size of the frame length header in bytes. Valid range: 0--4. Set to 0 to omit the header     |
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

**Reconnection backoff formula:** `delay = min(ReconnectInterval * 2^attempt, MaxReconnectDelay) + random jitter (0--25%)`

### Server Configuration

`ServerConfiguration` extends `ConnectorConfiguration`.

| Property         | Type  | Default | Description                                                                                                  |
|------------------|-------|---------|--------------------------------------------------------------------------------------------------------------|
| `MaxConnections` | `int` | `100`   | Maximum concurrent client connections. `0` = unlimited. Connections beyond this limit are immediately closed |

### Configuration Validation

Both configurations call `Validate()` at construction time and throw `ArgumentException` for invalid values:
- `MaxFrameLength` must be > 0
- `IdleTimeout` must be >= 0
- `WorkerThreadCount` must be >= 1
- `FrameLengthFieldLength` must be 0-4
- `FrameLengthFieldOffset` must be >= 0
- `ReconnectInterval` must be > 0
- `MaxReconnectDelay` must be > 0
- `MaxReconnectAttempts` must be >= 0

## TLS/SSL

TLS is configured via the `SslConfiguration` class, set on the `Ssl` property of either client or server configuration.

### SslConfiguration Properties

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

### IIso8583Metrics Methods

| Method                                       | Parameters                            | Called When                                                    |
|----------------------------------------------|---------------------------------------|----------------------------------------------------------------|
| `MessageSent(int mti)`                       | MTI of the outbound message           | A message is encoded and written to the wire (encoder)         |
| `MessageReceived(int mti)`                   | MTI of the inbound message            | A message is received and decoded from the wire (decoder)      |
| `MessageHandled(int mti, TimeSpan duration)` | MTI + time spent in the handler chain | A message handler chain completes successfully                 |
| `MessageError(int mti, Exception exception)` | MTI (0 if unknown) + the exception    | An error occurs during message handling                        |
| `ConnectionEstablished()`                    | --                                    | A new connection is opened (client connects or server accepts) |
| `ConnectionLost()`                           | --                                    | A connection is closed                                         |

### Integration Points

| Component                    | Metrics Calls                                                     |
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

| Order | Handler                                                                                | Added When                         |
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
        // Customize server bootstrap options
        bootstrap.ChildOption(ChannelOption.SoSndbuf, 65536);
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ServerConfiguration config)
    {
        // Add custom handlers after built-in pipeline
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
        // Customize client bootstrap options
        bootstrap.Option(ChannelOption.SoSndbuf, 65536);
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ClientConfiguration config)
    {
        // Add custom handlers after built-in pipeline
        pipeline.AddLast("myHandler", new MyCustomHandler());
    }
}

var client = new Iso8583Client<IsoMessage>(config, messageFactory, configurator: new MyClientConfigurator());
```

## Logging

The library depends on `ILogger` from `Microsoft.Extensions.Logging` -- bring your own provider (NLog, Serilog, console, etc.).

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

## Building

```bash
dotnet build Iso8583Suite.slnx
```

## Testing

```bash
dotnet test Iso8583Suite.slnx
```

149 tests, 90% method coverage.

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

## Contributing

Contributions are welcome! Please read the [contributing guide](CONTRIBUTING.md) for details on how to get started, submit pull requests, and follow the project's code guidelines.
