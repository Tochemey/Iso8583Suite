# Architecture

This document describes the internal architecture of Iso8583Suite. It is intended for contributors and maintainers who need to understand how the library is structured, how data flows through the pipeline, and the design decisions behind key components.

## Project Layout

```
Iso8583.Common/              Shared library: configuration, codecs, pipeline handlers, message factory
  Iso/                       Message factory abstraction, MTI construction, ISO 8583 version enums
  Metrics/                   Metrics interface and no-op implementation
  Netty/
    Codecs/                  Encoder, decoder, and frame decoder
    Pipelines/               Channel initializer and all pipeline handlers
Iso8583.Client/              TCP client with reconnection and request/response correlation
Iso8583.Server/              TCP server with connection tracking
Iso8583.Tests/               Unit and integration tests (xUnit)
Iso8583.Benchmarks/          Performance benchmarks (BenchmarkDotNet)
SampleServer/                Example authorization server
SampleClient/                Example authorization client
```

## Class Hierarchy

The connector hierarchy uses a layered generic design. The base class manages the SpanNetty event loops, channel lifecycle, and the message handler chain. The middle layer adds bootstrap configuration (client or server). The concrete classes add public API and feature-specific logic.

```
Iso8583Connector<T, TC>                          (abstract, Common)
|   Owns: CompositeIsoMessageHandler<T>, IMessageFactory<T>, TC configuration
|   Owns: BossEventLoopGroup, WorkerEventLoopGroup
|   Provides: AddMessageListener, RemoveMessageListener, channel get/set
|
+-- ClientConnector<T, TC>                       (abstract, Client)
|   |   Owns: Bootstrap (SpanNetty client bootstrap)
|   |   Sets: TCP_NODELAY, SO_KEEPALIVE, SO_REUSEADDR, AUTO_READ
|   |
|   +-- Iso8583Client<T>                         (concrete, Client)
|       Implements: IAsyncDisposable
|       Owns: PendingRequestManager<T>, ReconnectOnCloseHandler
|       Public API: Connect, Disconnect, Send, SendAndReceive, IsConnected
|
+-- ServerConnector<T, TC>                       (abstract, Server)
    |   Owns: ServerBootstrap (SpanNetty server bootstrap)
    |   Sets: SO_KEEPALIVE, SO_LINGER(5), TCP_NODELAY, SO_REUSEADDR
    |
    +-- Iso8583Server<T>                         (concrete, Server)
        Implements: IAsyncDisposable
        Owns: ConnectionTracker
        Public API: Start, Shutdown, ActiveConnectionCount, ActiveConnections
```

**Generic type parameters:**
- `T` is the ISO message type, constrained to `IsoMessage` (from NetCore8583).
- `TC` is the configuration type, constrained to `ConnectorConfiguration` or a subclass.

## Channel Pipeline

All network I/O flows through a SpanNetty channel pipeline configured by `Iso8583ChannelInitializer<TC>`. The pipeline is built once per connection in `InitChannel` and handlers execute in order:

```
Inbound (bytes from network -> application)          Outbound (application -> bytes to network)
==============================================       =============================================

[1] connectionTracker      (server only)             [7] iso8583Encoder
[2] tls                    (if SSL enabled)                 IsoMessage -> length header + wire bytes
[3] lengthFieldFrameDecoder                          [6] idleEventHandler
        raw bytes -> single message frame                   idle timeout -> echo request (outbound)
[4] iso8583Decoder                                   [5] messageHandler
        frame bytes -> IsoMessage object                    CompositeIsoMessageHandler dispatches
[5] iso8583Encoder                                          to registered IIsoMessageListener chain
        IsoMessage -> length header + wire bytes
[6] logging                (if enabled)
[7] replyOnError           (if enabled)
        ParseException -> error response
[8] idleState
        triggers IdleStateEvent after timeout
[9] idleEventHandler
        IdleStateEvent -> sends echo request
[10] messageHandler
        CompositeIsoMessageHandler
[11] reconnect             (client only)
        channel close -> schedule reconnect
```

The order matters. The frame decoder must run before the ISO decoder. The idle state detector must run before the idle event handler. The reconnect handler runs last so it fires after all other handlers have processed the channel-inactive event.

### Custom Pipeline Extension

Both `IClientConnectorConfigurator<T>` and `IServerConnectorConfigurator<T>` provide two hooks:
- `ConfigureBootstrap` -- add bootstrap options before the channel connects/binds.
- `ConfigurePipeline` -- append custom handlers to the pipeline after the standard handlers.

The configurator's `ConfigurePipeline` is called at the end of `InitChannel`, so custom handlers are appended after the built-in chain.

## Message Flow

### Inbound (receiving a message)

```
TCP bytes
  -> LengthFieldBasedFrameDecoder / StringLengthFieldBasedFrameDecoder
       Strips the length header, extracts a single message frame
  -> IsoMessageDecoder
       Calls IMessageFactory.ParseMessage(byte[], 0)
       Records MessageReceived metric
  -> CompositeIsoMessageHandler.ChannelRead
       Casts to T, dispatches to listener chain asynchronously
       For each listener: CanHandleMessage(msg) -> HandleMessage(ctx, msg)
       If HandleMessage returns false, the chain stops
       Records MessageHandled metric on success, MessageError on failure
```

### Outbound (sending a message)

```
IsoMessage
  -> IsoMessageEncoder.Encode
       Serializes the message via IsoMessage.WriteData() or WriteToBuffer()
       Prepends the length header (ASCII or binary, depending on config)
       Records MessageSent metric
  -> TCP bytes on the wire
```

### Request/Response Correlation (client only)

`Iso8583Client.SendAndReceive` provides correlated request/response semantics:

```
1. PendingRequestManager.RegisterPending(request, timeout)
     Builds correlation key: "{RequestMTI:X4}:{STAN}"   (e.g., "1100:100304")
     Stores a TaskCompletionSource<T> keyed by that string
     Registers timeout via CancellationTokenSource

2. Client sends the request via channel.WriteAndFlushAsync

3. When a response arrives, the inbound pipeline delivers it to
   CompositeIsoMessageHandler, which walks the listener chain.
   PendingRequestManager is registered as the FIRST listener.

4. PendingRequestManager.CanHandleMessage checks if the response
   has a matching pending key. It maps the response MTI back to
   the request MTI by subtracting 0x0010 (e.g., 0x1110 -> 0x1100).

5. PendingRequestManager.HandleMessage completes the TaskCompletionSource
   with the response, which unblocks the await in SendAndReceive.
   Returns false to stop further chain processing.

6. If no response arrives within the timeout, the CancellationTokenSource
   fires and the TaskCompletionSource is completed with a TimeoutException.
```

## Reconnection (client only)

When `AutoReconnect` is enabled, `Iso8583Client` creates a `ReconnectOnCloseHandler` and adds it as the last pipeline handler.

```
Channel becomes inactive
  -> ReconnectOnCloseHandler.ChannelInactive
       If max attempts reached, log error and give up
       Calculate delay: baseDelay * 2^attempt, capped at maxDelay, plus 0-25% jitter
       Schedule reconnect on the event loop after delay
         -> Iso8583Client.TryReconnect (under SemaphoreSlim lock)
              If not intentionally disconnected and channel is inactive:
                await Connect(host, port)   -- creates a fresh bootstrap + pipeline
              ReconnectOnCloseHandler.ResetAttempts() on success
```

Key design points:
- Exponential backoff with jitter prevents thundering-herd reconnection storms.
- The reconnect lock (`SemaphoreSlim`) prevents concurrent reconnection attempts.
- `_intentionalDisconnect` flag distinguishes user-initiated disconnect from connection loss.
- On successful reconnection, the attempt counter resets so transient failures don't exhaust the limit.

## Message Handler Chain

`CompositeIsoMessageHandler<T>` implements a copy-on-write pattern for thread-safe listener management:

- **Writes** (AddListener/RemoveListener) acquire a lock, copy the current listener array, modify the copy, and replace the reference atomically.
- **Reads** (ChannelRead -> DoHandleMessageAsync) snapshot the volatile array reference and iterate without locking.

This means handler registration/removal never blocks message processing, and message processing never blocks registration. The trade-off is a small allocation on each write, but writes (adding/removing listeners) are rare compared to reads (processing messages).

The chain walks listeners in registration order. Each listener's `CanHandleMessage(T)` is checked first. If it returns true, `HandleMessage(IChannelHandlerContext, T)` is called. If `HandleMessage` returns `false`, the chain stops -- no further listeners see the message. If it returns `true`, the next listener is tried.

When `failOnError` is true (the default), exceptions from listeners propagate up the pipeline and the channel is closed. When false, exceptions are logged and the chain continues.

## Connection Tracking (server only)

`ConnectionTracker` is a sharable pipeline handler added as the first handler in the server child pipeline. It uses `Interlocked` operations to maintain a connection count and a `ConcurrentDictionary` to track active channels.

When the count exceeds `MaxConnections`, the new channel is closed immediately before any other handler runs. This prevents overloaded servers from accepting more connections than configured.

## TLS

TLS is configured in `Iso8583ChannelInitializer.InitChannel` and added as the second handler (after connection tracking on the server). The handler is a SpanNetty `TlsHandler`:

- **Server**: loads a PKCS#12 certificate and optionally requires client certificates (`MutualTls`).
- **Client**: connects with `ClientTlsSettings`. For mutual TLS, loads a client certificate. `TargetHost` is used for SNI and certificate validation.

Certificate loading uses `X509CertificateLoader.LoadPkcs12FromFile` on .NET 9+ and the `X509Certificate2` constructor on older runtimes.

## Frame Encoding

ISO 8583 messages are length-prefixed on the wire. Two frame decoder strategies are supported:

- **Binary length header** (`EncodeFrameLengthAsString = false`): uses SpanNetty's built-in `LengthFieldBasedFrameDecoder`. The length is encoded as a big-endian integer.
- **ASCII length header** (`EncodeFrameLengthAsString = true`): uses `StringLengthFieldBasedFrameDecoder`, a custom decoder that parses the length from ASCII digits (e.g., "0152" for 152 bytes). This is zero-allocation: it reads individual bytes and computes the integer arithmetically.

On the encoding side, `IsoMessageEncoder` writes the length header before the message body. For ASCII encoding, it uses a `stackalloc` span to format digits without allocating.

## Metrics

`IIso8583Metrics` defines six hooks:
- `MessageSent(int mti)` -- called after encoding an outbound message.
- `MessageReceived(int mti)` -- called after decoding an inbound message.
- `MessageHandled(int mti, TimeSpan duration)` -- called after the handler chain completes successfully.
- `MessageError(int mti, Exception ex)` -- called when the handler chain throws.
- `ConnectionEstablished()` -- called when a channel becomes active.
- `ConnectionLost()` -- called when a channel becomes inactive.

When no metrics provider is configured, `NullIso8583Metrics.Instance` is used as a no-op singleton to avoid null checks throughout the pipeline.

## Message Factory

`IMessageFactory<T>` abstracts message creation and parsing. The default implementation, `IsoMessageFactory<T>`, wraps the NetCore8583 `MessageFactory<T>` and adds:

- MTI construction from enums: `NewMessage(MessageClass, MessageFunction, MessageOrigin)` builds the numeric MTI using `MTI.Value()` which combines the ISO version, class, function, and origin digits.
- Zero-copy byte conversion: `ParseMessage` reinterprets `byte[]` as `sbyte[]` via `Unsafe.As` (same memory layout) to bridge the NetCore8583 API without copying.

## Sensitive Data Masking

`IsoMessageLoggingHandler` masks sensitive fields in log output:
- Field 2 (PAN): first 6 and last 4 digits are shown, middle digits replaced with `*`.
- Fields 34, 35, 36, 45 (track data): replaced entirely with `***`.
- Custom fields can be specified via `SensitiveDataFields` in configuration.

When `LogSensitiveData` is true, all fields are logged in cleartext. This should only be used in development environments.

## Configuration Validation

`ConnectorConfiguration.Validate()` is called during construction and checks:
- `FrameLengthFieldLength` is between 0 and 4.
- `MaxFrameLength` is positive.
- `IdleTimeout` is non-negative.
- `WorkerThreadCount` is positive.

`ClientConfiguration.Validate()` additionally checks:
- `ReconnectInterval` is positive.
- `MaxReconnectDelay` >= `ReconnectInterval`.
- `MaxReconnectAttempts` is non-negative.

Invalid configuration throws `ArgumentOutOfRangeException` at startup, failing fast rather than at runtime.

## Thread Safety Summary

| Component                                   | Strategy                                                |
|---------------------------------------------|---------------------------------------------------------|
| `CompositeIsoMessageHandler` listener array | Copy-on-write with volatile reference                   |
| `ConnectionTracker` connection count        | `Interlocked.Increment`/`Decrement`                     |
| `ConnectionTracker` active channels         | `ConcurrentDictionary`                                  |
| `PendingRequestManager` pending map         | `ConcurrentDictionary<string, TaskCompletionSource<T>>` |
| `Iso8583Client` reconnect                   | `SemaphoreSlim(1,1)` + volatile flags                   |
| Channel I/O                                 | SpanNetty event loop (single-threaded per channel)      |
