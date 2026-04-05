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
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Metrics;
using Iso8583.Common.Netty.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetCore8583;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using LogLevel = DotNetty.Handlers.Logging.LogLevel;

namespace Iso8583.Server
{
  /// <summary>
  ///   An instance of this class will help bootstrap an iso 8583 server
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class Iso8583Server<T> : ServerConnector<T, ServerConfiguration>, IAsyncDisposable where T : IsoMessage
  {
    private readonly ILogger _logger;
    private readonly int _port;
    private readonly IIso8583Metrics _metrics;
    private readonly ConnectionTracker _connectionTracker;
    private volatile bool _disposed;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Server{T}" />
    /// </summary>
    /// <param name="port">the server port</param>
    /// <param name="configuration">the server configuration</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="logger">logger</param>
    /// <param name="configurator">optional pipeline/bootstrap configurator</param>
    /// <param name="metrics">optional metrics provider</param>
    public Iso8583Server(int port, ServerConfiguration configuration, IMessageFactory<T> messageFactory,
      ILogger logger = null,
      IServerConnectorConfigurator<ServerConfiguration> configurator = null,
      IIso8583Metrics metrics = null) : base(
      messageFactory, configuration)
    {
      _port = port;
      _logger = logger ?? NullLogger.Instance;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
      _connectionTracker = new ConnectionTracker(configuration.MaxConnections, _logger, _metrics);
      ConnectorConfigurator = configurator;
      CreateBossEventLoopGroup();
      CreateWorkerEventLoopGroup();
    }

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Server{T}" />
    /// </summary>
    /// <param name="port">the server port</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="logger">logger</param>
    public Iso8583Server(int port, IMessageFactory<T> messageFactory, ILogger logger = null) : base(
      messageFactory, new ServerConfiguration())
    {
      _port = port;
      _logger = logger ?? NullLogger.Instance;
      _metrics = NullIso8583Metrics.Instance;
      _connectionTracker = new ConnectionTracker(100, _logger, _metrics);
      CreateBossEventLoopGroup();
      CreateWorkerEventLoopGroup();
    }

    /// <summary>
    ///   Gets the number of currently active client connections.
    /// </summary>
    public int ActiveConnectionCount => _connectionTracker.ActiveConnectionCount;

    /// <summary>
    ///   Gets the currently active client channels.
    /// </summary>
    public IReadOnlyCollection<IChannel> ActiveConnections => _connectionTracker.ActiveChannels;

    /// <summary>
    ///   Creates and configures the DotNetty <see cref="ServerBootstrap"/> with child channel options
    ///   (keepalive, nodelay, linger), the ISO 8583 channel initializer, connection tracker, and metrics.
    /// </summary>
    protected override ServerBootstrap CreateBootstrap()
    {
      var boostrap = new ServerBootstrap();
      boostrap.Group(BossEventLoopGroup, WorkerEventLoopGroup)
        .ChildOption(ChannelOption.SoKeepalive, true)
        .ChildOption(ChannelOption.SoLinger, 5)
        .ChildOption(ChannelOption.TcpNodelay, true)
        .ChildOption(ChannelOption.SoReuseaddr, true)
        .Channel<TcpServerSocketChannel>()
        .Handler(new LoggingHandler(Configuration.LogLevel))
        .ChildHandler(new Iso8583ChannelInitializer<ServerConfiguration>(
          Configuration, ConnectorConfigurator, WorkerEventLoopGroup,
          MessageFactory as IMessageFactory<IsoMessage>, MessageHandler,
          isClient: false,
          connectionTracker: _connectionTracker, metrics: _metrics
        ));
      ConfigureBootstrap(boostrap);
      boostrap.Validate();
      return boostrap;
    }

    /// <summary>
    ///   starts the iso 8583 server
    /// </summary>
    public async Task Start()
    {
      ObjectDisposedException.ThrowIf(_disposed, this);

      Bootstrap = CreateBootstrap();
      var channel = await GetBootstrap().BindAsync(_port);
      SetChannel(channel);

      if (channel.IsOpen && channel.IsActive)
        _logger.LogInformation("Iso8583 Server started on: {Address}", channel.LocalAddress);
    }

    /// <summary>
    ///   shutdowns the iso server gracefully with a default 15-second grace period
    /// </summary>
    public async Task Shutdown()
    {
      await Shutdown(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    ///   shutdowns the iso server gracefully with a grace period for in-flight requests
    /// </summary>
    /// <param name="gracePeriod">time to wait for in-flight requests to complete</param>
    public async Task Shutdown(TimeSpan gracePeriod)
    {
      var channel = GetChannel();
      if (channel != null)
      {
        try { await channel.CloseAsync(); }
        catch { /* best effort */ }
      }

      // DotNetty's ShutdownGracefullyAsync may stall on some platforms when
      // child channels are still draining. We impose a hard outer timeout
      // so callers are never blocked indefinitely.
      var hardTimeout = gracePeriod.Add(TimeSpan.FromSeconds(2));
      try
      {
        var shutdownTask = Task.WhenAll(
          WorkerEventLoopGroup.ShutdownGracefullyAsync(
            TimeSpan.FromMilliseconds(100), gracePeriod),
          BossEventLoopGroup.ShutdownGracefullyAsync(
            TimeSpan.FromMilliseconds(100), gracePeriod));
        await Task.WhenAny(shutdownTask, Task.Delay(hardTimeout));
      }
      catch { /* suppress shutdown errors */ }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
      if (_disposed) return;
      _disposed = true;

      try
      {
        if (IsStarted())
          await Shutdown(TimeSpan.FromSeconds(5));
      }
      catch
      {
        // Best effort cleanup
      }

      GC.SuppressFinalize(this);
    }
  }
}
