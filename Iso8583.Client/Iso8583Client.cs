// Copyright 2021-2026 Arsene Tochemey Gandote
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   An instance of this class will help bootstrap an iso 8583 client
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class Iso8583Client<T> : ClientConnector<T, ClientConfiguration>, IAsyncDisposable
    where T : IsoMessage
  {
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private readonly PendingRequestManager<T> _pendingRequests = new();

    private string _host;
    private int _port;
    private volatile bool _intentionalDisconnect;
    private volatile bool _disposed;
    private ReconnectOnCloseHandler _reconnectHandler;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="configuration">the client configuration</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configurator">optional pipeline/bootstrap configurator</param>
    public Iso8583Client(ClientConfiguration configuration,
      IMessageFactory<T> messageFactory,
      IClientConnectorConfigurator<ClientConfiguration> configurator = null) : base(
      messageFactory, configuration)
    {
      ConnectorConfigurator = configurator;
      // Add correlation listener first so it gets first crack at responses
      MessageHandler.AddListener(_pendingRequests);
      CreateWorkerEventLoopGroup();
    }

    /// <summary>
    ///   create a new instance of  <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    public Iso8583Client(IMessageFactory<T> messageFactory) : base(messageFactory, new ClientConfiguration())
    {
      MessageHandler.AddListener(_pendingRequests);
      CreateWorkerEventLoopGroup();
    }

    /// <summary>
    ///   Sends an ISO 8583 message to the server (fire-and-forget).
    /// </summary>
    /// <param name="message">The ISO message to send.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected or the channel is inactive.</exception>
    public async Task Send(IsoMessage message)
    {
      ThrowIfDisposed();
      var channel = GetChannel()
                    ?? throw new InvalidOperationException("Client is not connected");

      if (!channel.IsActive)
        throw new InvalidOperationException("Channel is not active");

      await channel.WriteAndFlushAsync(message);
    }

    /// <summary>
    ///   Sends an ISO 8583 message to the server with a write timeout.
    /// </summary>
    /// <param name="message">The ISO message to send.</param>
    /// <param name="timeout">Maximum time in milliseconds to wait for the write to complete.</param>
    /// <exception cref="TimeoutException">Thrown when the send operation exceeds the timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the client is not connected or the channel is inactive.</exception>
    public async Task Send(IsoMessage message, int timeout)
    {
      ThrowIfDisposed();
      var channel = GetChannel()
                    ?? throw new InvalidOperationException("Client is not connected");

      if (!channel.IsActive)
        throw new InvalidOperationException("Channel is not active");

      var sendTask = channel.WriteAndFlushAsync(message);
      var completedTask = await Task.WhenAny(sendTask, Task.Delay(timeout));
      if (completedTask != sendTask)
        throw new TimeoutException($"Send operation timed out after {timeout}ms");

      await sendTask;
    }

    /// <summary>
    ///   Sends an ISO 8583 message and waits for the correlated response.
    ///   Correlation uses field 11 (STAN) and the message type.
    /// </summary>
    /// <param name="message">The ISO request message to send.</param>
    /// <param name="timeout">Maximum time to wait for a response.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The correlated response message.</returns>
    /// <exception cref="TimeoutException">Thrown when no response arrives within the timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered.</exception>
    public async Task<IsoMessage> SendAndReceive(IsoMessage message, TimeSpan timeout,
      CancellationToken cancellationToken = default)
    {
      ThrowIfDisposed();
      cancellationToken.ThrowIfCancellationRequested();

      // Register the pending request before sending (so we don't miss a fast response)
      var responseTask = _pendingRequests.RegisterPending((T)message, timeout, cancellationToken);

      try
      {
        await Send(message);
      }
      catch (Exception) when (cancellationToken.IsCancellationRequested)
      {
        _pendingRequests.CancelAll();
        throw new OperationCanceledException(cancellationToken);
      }
      catch
      {
        // If send fails, cancel the pending registration
        _pendingRequests.CancelAll();
        throw;
      }

      return await responseTask;
    }

    /// <summary>
    ///   Creates and configures a new SpanNetty <see cref="Bootstrap"/> with the channel initializer,
    ///   reconnection handler, and TCP socket options.
    /// </summary>
    private Bootstrap CreateBootstrap()
    {
      // Create reconnect handler if auto-reconnect is enabled
      if (Configuration.AutoReconnect)
      {
        _reconnectHandler = new ReconnectOnCloseHandler(
          TryReconnect,
          Configuration.ReconnectInterval,
          Configuration.MaxReconnectDelay,
          Configuration.MaxReconnectAttempts
        );
      }

      var bootstrap = new Bootstrap();
      bootstrap.Group(WorkerEventLoopGroup);
      bootstrap.Channel<TcpSocketChannel>();
      bootstrap.Handler(new Iso8583ChannelInitializer<ClientConfiguration>(
        Configuration, ConnectorConfigurator, WorkerEventLoopGroup,
        MessageFactory as IMessageFactory<IsoMessage>, MessageHandler,
        _reconnectHandler
      ));

      ConfigureBootstrap(bootstrap);
      bootstrap.Validate();
      return bootstrap;
    }

    /// <summary>
    ///   connects to the iso8583 server
    /// </summary>
    /// <param name="host">the iso server host (IP address or hostname)</param>
    /// <param name="port">the iso server port</param>
    public async Task Connect(string host, int port)
    {
      ThrowIfDisposed();
      _intentionalDisconnect = false;
      _host = host;
      _port = port;

      Bootstrap = CreateBootstrap();

      // resolve host to IP address (supports both IP and DNS hostnames)
      if (!IPAddress.TryParse(host, out var ipAddress))
      {
        var addresses = await Dns.GetHostAddressesAsync(host);
        if (addresses.Length == 0)
          throw new ArgumentException($"Cannot resolve hostname: {host}");
        ipAddress = addresses[0];
      }

      var channel = await GetBootstrap().ConnectAsync(new IPEndPoint(ipAddress, port));
      SetChannel(channel);

      // Reset reconnect counter on successful connection
      _reconnectHandler?.ResetAttempts();
    }

    /// <summary>
    ///   disconnects from the iso8583 server
    /// </summary>
    public async Task Disconnect()
    {
      _intentionalDisconnect = true;
      _pendingRequests.CancelAll();
      var channel = GetChannel();
      if (channel != null)
      {
        try { await channel.CloseAsync(); }
        catch { /* best effort */ }
      }

      // Use a 1-second quiet period so the event loop stays alive long
      // enough for any in-flight socket I/O completion callbacks that
      // arrive after CloseAsync returns; this prevents an unhandled
      // RejectedExecutionException on thread-pool threads.
      try
      {
        await WorkerEventLoopGroup.ShutdownGracefullyAsync(
          TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
      }
      catch
      {
        // Suppress event loop shutdown errors
      }
    }

    /// <summary>
    ///   checks whether the client is connected to the iso8583 server
    /// </summary>
    public bool IsConnected()
    {
      var channel = GetChannel();
      return channel is { IsActive: true };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
      if (_disposed) return;
      _disposed = true;

      try { await Disconnect(); }
      catch { /* best effort */ }

      _reconnectLock.Dispose();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    ///   Attempts to reconnect to the server. Called by <see cref="ReconnectOnCloseHandler"/>.
    /// </summary>
    private async Task TryReconnect()
    {
      if (_intentionalDisconnect || _disposed) return;

      await _reconnectLock.WaitAsync();
      try
      {
        if (_intentionalDisconnect || _disposed) return;
        if (!IsChannelInactive()) return;

        await Connect(_host, _port);
      }
      finally
      {
        _reconnectLock.Release();
      }
    }

    /// <summary>
    ///   Throws <see cref="ObjectDisposedException"/> if the client has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
    }
  }
}
