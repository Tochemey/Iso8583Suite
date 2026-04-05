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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using DotNetty.Transport.Channels;
using Iso8583.Common.Metrics;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Tracks active connections and enforces a maximum connection limit.
  ///   Add this handler to the server child pipeline.
  /// </summary>
  public class ConnectionTracker : ChannelHandlerAdapter
  {
    private readonly int _maxConnections;
    private readonly ILogger _logger;
    private readonly IIso8583Metrics _metrics;
    private readonly ConcurrentDictionary<IChannel, byte> _activeChannels = new();
    private int _connectionCount;

    /// <summary>
    ///   Creates a new connection tracker.
    /// </summary>
    /// <param name="maxConnections">maximum connections (0 for unlimited)</param>
    /// <param name="logger">optional logger</param>
    /// <param name="metrics">optional metrics provider</param>
    public ConnectionTracker(int maxConnections = 0, ILogger logger = null, IIso8583Metrics metrics = null)
    {
      _maxConnections = maxConnections;
      _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
    }

    /// <summary>
    ///   Gets the current number of active connections.
    /// </summary>
    public int ActiveConnectionCount => _connectionCount;

    /// <summary>
    ///   Gets a snapshot of all currently active channels.
    /// </summary>
    public IReadOnlyCollection<IChannel> ActiveChannels => (IReadOnlyCollection<IChannel>)_activeChannels.Keys;

    /// <inheritdoc />
    public override bool IsSharable => true;

    /// <summary>
    ///   Increments the connection count when a new channel becomes active.
    ///   If the maximum connection limit is exceeded, the channel is closed immediately.
    /// </summary>
    public override void ChannelActive(IChannelHandlerContext context)
    {
      var count = Interlocked.Increment(ref _connectionCount);

      if (_maxConnections > 0 && count > _maxConnections)
      {
        // Do NOT decrement here. ChannelInactive will fire when CloseAsync
        // completes and will perform the decrement, keeping the count correct.
        _logger.LogWarning("Max connections ({Max}) exceeded. Rejecting connection from {Remote}",
          _maxConnections, context.Channel.RemoteAddress);
        context.CloseAsync();
        return;
      }

      _activeChannels.TryAdd(context.Channel, 0);
      _metrics.ConnectionEstablished();
      _logger.LogDebug("Connection established from {Remote}. Active: {Count}",
        context.Channel.RemoteAddress, count);

      base.ChannelActive(context);
    }

    /// <summary>
    ///   Decrements the connection count and removes the channel from the active set when it becomes inactive.
    /// </summary>
    public override void ChannelInactive(IChannelHandlerContext context)
    {
      _activeChannels.TryRemove(context.Channel, out _);
      var count = Interlocked.Decrement(ref _connectionCount);
      _metrics.ConnectionLost();
      _logger.LogDebug("Connection closed from {Remote}. Active: {Count}",
        context.Channel.RemoteAddress, count);

      base.ChannelInactive(context);
    }
  }
}
