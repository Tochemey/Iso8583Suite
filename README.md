# Iso8583Suite

A high-performance .NET TCP client/server library for [ISO 8583](https://en.wikipedia.org/wiki/ISO_8583) financial transaction messaging, built on top of [NetCore8583](https://github.com/Tochemey/NetCore8583) and [SpanNetty](https://github.com/cuteant/SpanNetty) (DotNetty).

## Features

- **TCP Server & Client** with async I/O via SpanNetty (DotNetty)
- **Request/Response Correlation** - `SendAndReceive` with STAN-based matching and configurable timeouts
- **Auto-Reconnection** with exponential backoff and jitter
- **TLS/SSL Support** with mutual TLS option
- **Message Listener Chain** - composable handler pipeline with thread-safe copy-on-write dispatch
- **ISO 8583 Message Factory** - create, parse, and respond to messages using MTI components
- **Configurable Frame Encoding** - ASCII or binary length headers
- **Connection Management** - max connections, active connection tracking
- **Metrics/Observability Hooks** - plug in Prometheus, OpenTelemetry, or any provider
- **Sensitive Data Masking** - configurable PAN and track data masking in logs
- **Idle Detection & Echo** - automatic keepalive via network management messages
- **Graceful Shutdown** - configurable drain period for in-flight requests
- **`IAsyncDisposable`** - proper resource cleanup on both client and server
- **Multi-target** - .NET 8.0 and .NET 10.0

## Project Structure

```
Iso8583Suite.slnx
  Iso8583.Common/        Core library: codecs, pipeline handlers, configuration
  Iso8583.Client/        TCP client with reconnection and request correlation
  Iso8583.Server/        TCP server with connection management
  Iso8583.Tests/         Unit tests (xUnit)
  Iso8583.Benchmarks/    Performance benchmarks (BenchmarkDotNet)
  SampleServer/          Example authorization server
  SampleClient/          Example authorization client
```

## Quick Start

### Install

Reference the library projects directly, or package them as NuGet packages for your solution.

### Server

```csharp
using Iso8583.Common.Iso;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;

// Create and configure the message factory
var mfact = ConfigParser.CreateDefault();
ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
mfact.UseBinaryMessages = false;
mfact.Encoding = Encoding.ASCII;

var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

// Configure the server
var config = new ServerConfiguration
{
    EncodeFrameLengthAsString = true,
    FrameLengthFieldLength = 4,
    MaxConnections = 100,
    AddLoggingHandler = true,
    LogSensitiveData = false  // mask PAN/track data in production
};

// Create and start
await using var server = new Iso8583Server<IsoMessage>(9000, config, messageFactory, logger);
server.AddMessageListener(new AuthorizationHandler(messageFactory));
await server.Start();

// Monitor connections
Console.WriteLine($"Active connections: {server.ActiveConnectionCount}");

// Graceful shutdown with 15-second drain
await server.Shutdown();
```

### Message Handler

```csharp
public class AuthorizationHandler : IIsoMessageListener<IsoMessage>
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public AuthorizationHandler(IsoMessageFactory<IsoMessage> factory) => _factory = factory;

    // Only handle authorization requests (0x1100)
    public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x1100;

    public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
    {
        var response = _factory.CreateResponse(request);
        response.CopyFieldsFrom(request, 2, 3, 4, 7, 11, 12, 37, 41, 42, 49);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "123456", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3)); // Approved

        await context.WriteAndFlushAsync(response);
        return false; // stop chain - we handled it
    }
}
```

### Client

```csharp
using Iso8583.Client;
using Iso8583.Common.Iso;
using NetCore8583;

var config = new ClientConfiguration
{
    EncodeFrameLengthAsString = true,
    FrameLengthFieldLength = 4,
    AutoReconnect = true,
    ReconnectInterval = 500,       // 500ms base delay
    MaxReconnectAttempts = 10,
    MaxReconnectDelay = 30000      // 30s max backoff
};

await using var client = new Iso8583Client<IsoMessage>(config, messageFactory);
await client.Connect("payment-gateway.internal", 9000);

// Build an authorization request
var request = messageFactory.NewMessage(0x1100);
request.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
request.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
request.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
request.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
// ... more fields

// Send and wait for correlated response (matched by STAN field 11)
var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(10));
var responseCode = response.GetField(39)?.Value; // "000" = approved
```

## Configuration

### ConnectorConfiguration (base for both client and server)

| Property                    | Default       | Description                                     |
|-----------------------------|---------------|-------------------------------------------------|
| `EncodeFrameLengthAsString` | `false`       | ASCII (`"0152"`) vs binary length header        |
| `FrameLengthFieldLength`    | `2`           | Length header size in bytes (0-4)               |
| `MaxFrameLength`            | `8192`        | Maximum message size in bytes                   |
| `IdleTimeout`               | `30`          | Seconds before echo keepalive is sent           |
| `WorkerThreadCount`         | `CPU * 2`     | SpanNetty event loop threads                    |
| `AddLoggingHandler`         | `false`       | Enable message logging in pipeline              |
| `LogSensitiveData`          | `true`        | Show PAN/track data unmasked in logs            |
| `SensitiveDataFields`       | `34,35,36,45` | Fields to mask when `LogSensitiveData` is false |
| `ReplyOnError`              | `false`       | Send error response on parse failures           |
| `AddEchoMessageListener`    | `false`       | Auto-respond to echo requests                   |
| `Ssl`                       | `null`        | TLS/SSL configuration (see below)               |

### ClientConfiguration (extends above)

| Property               | Default | Description                       |
|------------------------|---------|-----------------------------------|
| `AutoReconnect`        | `true`  | Auto-reconnect on connection loss |
| `ReconnectInterval`    | `100`   | Base reconnect delay in ms        |
| `MaxReconnectDelay`    | `30000` | Maximum backoff delay in ms       |
| `MaxReconnectAttempts` | `10`    | Max retries (0 = unlimited)       |

### ServerConfiguration (extends base)

| Property         | Default | Description                            |
|------------------|---------|----------------------------------------|
| `MaxConnections` | `100`   | Max concurrent clients (0 = unlimited) |

### TLS/SSL

```csharp
var config = new ServerConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        CertificatePath = "/path/to/server.pfx",
        CertificatePassword = "password",
        MutualTls = true  // require client certificates
    }
};
```

Client-side:
```csharp
var config = new ClientConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        TargetHost = "payment-gateway.internal",
        // For mutual TLS:
        MutualTls = true,
        CertificatePath = "/path/to/client.pfx",
        CertificatePassword = "password"
    }
};
```

## Metrics

Implement `IIso8583Metrics` to integrate with your observability stack:

```csharp
public class PrometheusMetrics : IIso8583Metrics
{
    private static readonly Counter MessagesSent = Metrics.CreateCounter("iso8583_messages_sent", "Messages sent", "mti");
    private static readonly Counter MessagesReceived = Metrics.CreateCounter("iso8583_messages_received", "Messages received", "mti");
    private static readonly Histogram HandleDuration = Metrics.CreateHistogram("iso8583_handle_duration_seconds", "Handler duration");

    public void MessageSent(int mti) => MessagesSent.WithLabels(mti.ToString("X4")).Inc();
    public void MessageReceived(int mti) => MessagesReceived.WithLabels(mti.ToString("X4")).Inc();
    public void MessageHandled(int mti, TimeSpan duration) => HandleDuration.Observe(duration.TotalSeconds);
    public void MessageError(int mti, Exception ex) { /* ... */ }
    public void ConnectionEstablished() { /* ... */ }
    public void ConnectionLost() { /* ... */ }
}

// Pass to server
var server = new Iso8583Server<IsoMessage>(9000, config, factory, logger, metrics: new PrometheusMetrics());
```

## Pipeline Architecture

The SpanNetty channel pipeline is configured as:

```
[TLS Handler]              (optional, if SSL enabled)
[Connection Tracker]       (server only, enforces MaxConnections)
[Frame Decoder]            (ASCII or binary length-field based)
[ISO 8583 Decoder]         (bytes -> IsoMessage)
[ISO 8583 Encoder]         (IsoMessage -> bytes)
[Logging Handler]          (optional, formats and masks messages)
[Parse Exception Handler]  (optional, sends error responses)
[Idle State Handler]       (triggers after IdleTimeout seconds)
[Idle Event Handler]       (sends echo keepalive on idle)
[Message Handler]          (CompositeIsoMessageHandler -> listener chain)
[Reconnect Handler]        (client only, exponential backoff)
```

## Performance

Benchmarked on Apple M1, .NET 10.0.2:

| Operation                              |        Throughput | Memory/op |
|----------------------------------------|------------------:|----------:|
| Create message + 15 fields             |  ~1,819,000 msg/s |   2.29 KB |
| Create response from request           |  ~1,126,000 msg/s |   2.27 KB |
| Encode message (optimized)             |    ~111,600 msg/s |   3.69 KB |
| Request/response correlation cycle     | ~510,000 cycles/s |   1.18 KB |
| Frame length parse (optimized, 4-byte) |    ~143,000,000/s |       0 B |

### Optimization Highlights

| Component         | Technique                                                | Improvement                 |
|-------------------|----------------------------------------------------------|-----------------------------|
| Frame decoder     | `GetByte()` arithmetic vs `byte[] + string`              | **3-5x faster, zero alloc** |
| Message encoder   | `Buffer.BlockCopy` + stackalloc vs `ToString + GetBytes` | **12% less memory**         |
| Message decoder   | `Unsafe.As` reinterpret vs `ToInt8()` copy               | **Eliminates array copy**   |
| Listener dispatch | Copy-on-write `volatile` array                           | **Lock-free reads**         |

Run benchmarks:
```bash
dotnet run --project Iso8583.Benchmarks -c Release
# or specific benchmark:
dotnet run --project Iso8583.Benchmarks -c Release -- --filter '*EncoderBenchmarks*'
```

## Building

```bash
dotnet build Iso8583Suite.slnx
```

## Testing

```bash
dotnet test Iso8583Suite.slnx
```

## Running the Samples

Terminal 1 (server):
```bash
dotnet run --project SampleServer
```

Terminal 2 (client):
```bash
dotnet run --project SampleClient
```

The client sends an authorization request (0x1100), the server processes it and returns an authorization response (0x1110) with response code "000" (approved).

## Dependencies

| Package                                                | Version       | Purpose                                   |
|--------------------------------------------------------|---------------|-------------------------------------------|
| [NetCore8583](https://github.com/Tochemey/NetCore8583) | 2.4.0         | ISO 8583 message parsing and construction |
| [SpanNetty](https://github.com/cuteant/SpanNetty)      | 0.7.2012.2221 | Async TCP I/O (DotNetty fork)             |
| Microsoft.Extensions.Logging.Abstractions              | 10.0.2        | Logging abstraction                       |

The library uses `Microsoft.Extensions.Logging.ILogger` - bring your own logging provider (NLog, Serilog, etc.).

## License

See [LICENSE](LICENSE) for details.
