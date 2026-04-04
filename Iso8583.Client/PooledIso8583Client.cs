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
using System.Threading;
using System.Threading.Tasks;
using Iso8583.Common;
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   A connection-pooled ISO 8583 client that maintains multiple persistent connections
  ///   and distributes requests across them using a configurable <see cref="ILoadBalancer"/> strategy.
  ///   Drop-in replacement for <see cref="Iso8583Client{T}"/> in high-throughput scenarios.
  /// </summary>
  /// <typeparam name="T">The ISO message type.</typeparam>
  public sealed class PooledIso8583Client<T> : IAsyncDisposable
    where T : IsoMessage
  {
    // Pool sizes above this threshold fall back to heap allocation in the hot path to avoid stack overflow.
    private const int StackAllocThreshold = 64;

    private readonly Iso8583Client<T>[] _clients;
    private readonly PooledClientConfiguration _configuration;
    private readonly ILoadBalancer _loadBalancer;
    private readonly IMessageFactory<T> _messageFactory;
    private readonly IClientConnectorConfigurator<ClientConfiguration> _configurator;
    private readonly Timer _healthCheckTimer;
    private readonly object _listenersLock = new();
    private readonly List<IIsoMessageListener<T>> _listeners = new();

    private volatile bool _disposed;
    private string _host;
    private int _port;

    /// <summary>
    ///   Creates a new instance of <see cref="PooledIso8583Client{T}"/>.
    /// </summary>
    /// <param name="configuration">the pooled client configuration</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="loadBalancer">the load balancing strategy (defaults to round-robin)</param>
    /// <param name="configurator">optional pipeline/bootstrap configurator applied to each connection</param>
    public PooledIso8583Client(
      PooledClientConfiguration configuration,
      IMessageFactory<T> messageFactory,
      ILoadBalancer loadBalancer = null,
      IClientConnectorConfigurator<ClientConfiguration> configurator = null)
    {
      configuration.Validate();

      _configuration = configuration;
      _messageFactory = messageFactory;
      _configurator = configurator;
      _clients = new Iso8583Client<T>[configuration.PoolSize];
      _loadBalancer = loadBalancer ?? new RoundRobinLoadBalancer();

      for (var i = 0; i < _clients.Length; i++)
        _clients[i] = CreateClient();

      _healthCheckTimer = new Timer(
        static state => _ = ((PooledIso8583Client<T>)state!).RunHealthCheckAsync(),
        this,
        Timeout.InfiniteTimeSpan,
        Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    ///   Gets the number of connections in the pool.
    /// </summary>
    public int PoolSize => _clients.Length;

    /// <summary>
    ///   Gets the number of currently connected (healthy) connections.
    /// </summary>
    public int ActiveConnectionCount
    {
      get
      {
        var count = 0;
        for (var i = 0; i < _clients.Length; i++)
        {
          if (_clients[i].IsConnected())
            count++;
        }

        return count;
      }
    }

    /// <summary>
    ///   Connects all pooled connections to the specified server.
    /// </summary>
    /// <param name="host">the iso server host (IP address or hostname)</param>
    /// <param name="port">the iso server port</param>
    public async Task Connect(string host, int port)
    {
      ThrowIfDisposed();
      _host = host;
      _port = port;

      var tasks = new Task[_clients.Length];
      for (var i = 0; i < _clients.Length; i++)
        tasks[i] = _clients[i].Connect(host, port);

      await Task.WhenAll(tasks);

      _healthCheckTimer.Change(
        _configuration.HealthCheckInterval,
        _configuration.HealthCheckInterval);
    }

    /// <summary>
    ///   Disconnects all pooled connections.
    /// </summary>
    public async Task Disconnect()
    {
      _healthCheckTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

      var tasks = new Task[_clients.Length];
      for (var i = 0; i < _clients.Length; i++)
        tasks[i] = _clients[i].Disconnect();

      await Task.WhenAll(tasks);
    }

    /// <summary>
    ///   Sends an ISO 8583 message using a load-balanced connection (fire-and-forget).
    /// </summary>
    /// <param name="message">The ISO message to send.</param>
    /// <exception cref="InvalidOperationException">Thrown when no healthy connections are available.</exception>
    public async Task Send(IsoMessage message)
    {
      ThrowIfDisposed();
      var client = SelectClient();
      await client.Send(message);
    }

    /// <summary>
    ///   Sends an ISO 8583 message with a write timeout using a load-balanced connection.
    /// </summary>
    /// <param name="message">The ISO message to send.</param>
    /// <param name="timeout">Maximum time in milliseconds to wait for the write to complete.</param>
    /// <exception cref="TimeoutException">Thrown when the send operation exceeds the timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no healthy connections are available.</exception>
    public async Task Send(IsoMessage message, int timeout)
    {
      ThrowIfDisposed();
      var client = SelectClient();
      await client.Send(message, timeout);
    }

    /// <summary>
    ///   Sends an ISO 8583 message and waits for the correlated response using a load-balanced connection.
    /// </summary>
    /// <param name="message">The ISO request message to send.</param>
    /// <param name="timeout">Maximum time to wait for a response.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The correlated response message.</returns>
    /// <exception cref="TimeoutException">Thrown when no response arrives within the timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no healthy connections are available.</exception>
    public async Task<IsoMessage> SendAndReceive(IsoMessage message, TimeSpan timeout,
      CancellationToken cancellationToken = default)
    {
      ThrowIfDisposed();
      var client = SelectClient();
      return await client.SendAndReceive(message, timeout, cancellationToken);
    }

    /// <summary>
    ///   Adds a message listener to all connections in the pool (and to any connection created
    ///   later during health-check recovery).
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void AddMessageListener(IIsoMessageListener<T> handler)
    {
      // Hold the lock across the list mutation AND the propagation loop so that a concurrent
      // health-check replacement cannot create a fresh client, copy the (old) listener list,
      // and then race with us adding the listener to that same new client (which would duplicate
      // the registration). CreateClient takes the same lock, so it either sees this listener in
      // the list or is fully serialized after us.
      lock (_listenersLock)
      {
        _listeners.Add(handler);
        for (var i = 0; i < _clients.Length; i++)
          _clients[i].AddMessageListener(handler);
      }
    }

    /// <summary>
    ///   Removes a message listener from all connections in the pool.
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void RemoveMessageListener(IIsoMessageListener<T> handler)
    {
      lock (_listenersLock)
      {
        _listeners.Remove(handler);
        for (var i = 0; i < _clients.Length; i++)
          _clients[i].RemoveMessageListener(handler);
      }
    }

    /// <summary>
    ///   Gets the number of pending (in-flight) requests for a specific connection index.
    /// </summary>
    /// <param name="index">the connection index</param>
    /// <returns>the pending request count</returns>
    internal int GetPendingCount(int index) => _clients[index].PendingCount;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
      if (_disposed) return;
      _disposed = true;

      await _healthCheckTimer.DisposeAsync();

      var tasks = new ValueTask[_clients.Length];
      for (var i = 0; i < _clients.Length; i++)
        tasks[i] = _clients[i].DisposeAsync();

      for (var i = 0; i < tasks.Length; i++)
        await tasks[i];
    }

    private Iso8583Client<T> CreateClient()
    {
      var client = new Iso8583Client<T>(_configuration.ClientConfiguration, _messageFactory, _configurator);

      // Snapshot and copy registered listeners under the lock (AddMessageListener also holds
      // this lock while mutating both the list and the existing clients, so we either see the
      // listener here or it has not been added yet and will be added to this client by the
      // concurrent call).
      lock (_listenersLock)
      {
        foreach (var listener in _listeners)
          client.AddMessageListener(listener);
      }

      return client;
    }

    /// <summary>
    ///   Chooses an active connection via the load balancer. Uses a stack-allocated buffer for
    ///   small pools and a heap allocation as fallback for large ones, keeping the hot path
    ///   allocation-free in the common case.
    /// </summary>
    private Iso8583Client<T> SelectClient()
    {
      var poolSize = _clients.Length;
      Span<int> buffer = poolSize <= StackAllocThreshold
        ? stackalloc int[poolSize]
        : new int[poolSize];

      var activeCount = 0;
      for (var i = 0; i < poolSize; i++)
      {
        if (_clients[i].IsConnected())
          buffer[activeCount++] = i;
      }

      if (activeCount == 0)
        throw new InvalidOperationException("No active connections available");

      var index = _loadBalancer.Select(buffer[..activeCount]);
      return _clients[index];
    }

    /// <summary>
    ///   Timer entry point that runs the health check and reports any unexpected failure
    ///   to avoid unobserved task exceptions.
    /// </summary>
    private async Task RunHealthCheckAsync()
    {
      try
      {
        await CheckHealthAsync();
      }
      catch
      {
        // Swallow to prevent unobserved task exceptions from the timer thread.
        // Per-client replacement failures are already handled inside CheckHealthAsync;
        // this catch only covers truly unexpected errors (e.g., if the pool is disposed
        // concurrently with a health check in flight).
      }
    }

    private async Task CheckHealthAsync()
    {
      if (_disposed || _host == null) return;

      for (var i = 0; i < _clients.Length; i++)
      {
        if (_disposed) return;

        if (_clients[i].IsConnected()) continue;

        // Replace dead connection atomically: create, connect, swap, then dispose the old one.
        // Swapping the slot BEFORE disposing the old client ensures concurrent SelectClient
        // calls always observe either the old (possibly inactive) or the new (active) reference,
        // never a disposed one.
        var replacement = CreateClient();
        try
        {
          await replacement.Connect(_host, _port);
        }
        catch
        {
          // Failed to connect the replacement — clean it up and retry on the next cycle.
          try { await replacement.DisposeAsync(); } catch { /* best effort */ }
          continue;
        }

        var old = Interlocked.Exchange(ref _clients[i], replacement);
        try { await old.DisposeAsync(); } catch { /* best effort */ }
      }
    }

    private void ThrowIfDisposed()
    {
      ObjectDisposedException.ThrowIf(_disposed, this);
    }
  }
}
