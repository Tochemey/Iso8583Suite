# Iso8583Suite

[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/Tochemey/Iso8583Suite/ci.yml)](https://github.com/Tochemey/Iso8583Suite/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

A high-performance .NET TCP client and server library for [ISO 8583](https://en.wikipedia.org/wiki/ISO_8583) financial messaging, built on [NetCore8583](https://github.com/Tochemey/NetCore8583) and [SpanNetty](https://github.com/cuteant/SpanNetty).

Targets **.NET 8**, **.NET 9**, and **.NET 10**.

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

## Getting Started

### Server

```csharp
var mfact = ConfigParser.CreateDefault();
ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
mfact.UseBinaryMessages = false;
mfact.Encoding = Encoding.ASCII;
var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

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
```

### Message Handler

Implement `IIsoMessageListener<T>` to handle specific message types:

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
        return false; // stop handler chain
    }
}
```

Return `false` to stop the chain, `true` to pass the message to the next listener.

### Client

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

var request = messageFactory.NewMessage(0x1100);
request.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
request.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
request.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
request.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));

// Send and wait for correlated response (matched by STAN field 11)
var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(10));
var responseCode = response.GetField(39)?.Value; // "000" = approved
```

`SendAndReceive` correlates request and response using field 11 (STAN) and the message type. If no response arrives within the timeout, it throws `TimeoutException`.

For fire-and-forget:

```csharp
await client.Send(request);
```

## Configuration

### Base Configuration

All options below apply to both client and server:

| Property                    | Default       | Description                                                         |
|-----------------------------|---------------|---------------------------------------------------------------------|
| `EncodeFrameLengthAsString` | `false`       | `true` for ASCII length headers (e.g. `"0152"`), `false` for binary |
| `FrameLengthFieldLength`    | `2`           | Length of the frame header (0-4 bytes)                              |
| `MaxFrameLength`            | `8192`        | Maximum message size in bytes                                       |
| `IdleTimeout`               | `30`          | Seconds of inactivity before sending an echo keepalive              |
| `WorkerThreadCount`         | `CPU * 2`     | SpanNetty event loop threads                                        |
| `AddLoggingHandler`         | `false`       | Add message logging to the pipeline                                 |
| `LogSensitiveData`          | `true`        | Show PAN/track data unmasked (set `false` in production)            |
| `SensitiveDataFields`       | `34,35,36,45` | Fields to mask when `LogSensitiveData` is `false`                   |
| `ReplyOnError`              | `false`       | Send administrative error response on parse failures                |
| `AddEchoMessageListener`    | `false`       | Auto-respond to 0x0800 echo requests                                |
| `Ssl`                       | `null`        | TLS configuration (see below)                                       |

### Client Configuration

| Property               | Default | Description                                    |
|------------------------|---------|------------------------------------------------|
| `AutoReconnect`        | `true`  | Reconnect automatically on connection loss     |
| `ReconnectInterval`    | `100`   | Base delay in ms (exponential backoff applied) |
| `MaxReconnectDelay`    | `30000` | Maximum backoff delay in ms                    |
| `MaxReconnectAttempts` | `10`    | Max retry count (`0` = unlimited)              |

### Server Configuration

| Property         | Default | Description                                  |
|------------------|---------|----------------------------------------------|
| `MaxConnections` | `100`   | Maximum concurrent clients (`0` = unlimited) |

### TLS

```csharp
// Server
var config = new ServerConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        CertificatePath = "/path/to/server.pfx",
        CertificatePassword = "password",
        MutualTls = true
    }
};

// Client
var config = new ClientConfiguration
{
    Ssl = new SslConfiguration
    {
        Enabled = true,
        TargetHost = "payment-gateway.internal",
        MutualTls = true,
        CertificatePath = "/path/to/client.pfx",
        CertificatePassword = "password"
    }
};
```

## Metrics

Implement `IIso8583Metrics` and pass it to the server constructor:

```csharp
public class MyMetrics : IIso8583Metrics
{
    public void MessageSent(int mti) { /* counter++ */ }
    public void MessageReceived(int mti) { /* counter++ */ }
    public void MessageHandled(int mti, TimeSpan duration) { /* histogram.observe(duration) */ }
    public void MessageError(int mti, Exception ex) { /* counter++ */ }
    public void ConnectionEstablished() { /* gauge++ */ }
    public void ConnectionLost() { /* gauge-- */ }
}

var server = new Iso8583Server<IsoMessage>(9000, config, factory, logger, metrics: new MyMetrics());
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

## Running the Samples

```bash
# Terminal 1
dotnet run --project SampleServer

# Terminal 2
dotnet run --project SampleClient
```

The client sends an authorization request (0x1100), the server responds with 0x1110 (approved).

The library depends on `ILogger` from `Microsoft.Extensions.Logging` -- bring your own provider (NLog, Serilog, console, etc.).

## License

[Apache License 2.0](LICENSE)
