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
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Client-side handler that triggers automatic reconnection with exponential backoff
  ///   when the channel is closed unexpectedly. When a reconnection attempt fails (e.g.,
  ///   the remote host is unreachable and no new channel is created), the handler
  ///   self-schedules the next attempt on a thread-pool timer so the retry loop survives
  ///   the death of the original event loop.
  /// </summary>
  public class ReconnectOnCloseHandler : ChannelHandlerAdapter
  {
    private readonly Func<Task> _reconnectFunc;
    private readonly int _baseDelay;
    private readonly int _maxDelay;
    private readonly int _maxAttempts;
    private readonly ILogger _logger;
    private int _attempt;
    private volatile bool _stopped;

    /// <summary>
    ///   Creates a new instance of <see cref="ReconnectOnCloseHandler"/>.
    /// </summary>
    /// <param name="reconnectFunc">Delegate that performs the reconnection attempt</param>
    /// <param name="baseDelay">Base delay in milliseconds before the first retry</param>
    /// <param name="maxDelay">Maximum delay in milliseconds between retries</param>
    /// <param name="maxAttempts">Maximum number of reconnection attempts (0 for unlimited)</param>
    /// <param name="logger">Logger instance</param>
    public ReconnectOnCloseHandler(Func<Task> reconnectFunc, int baseDelay,
      int maxDelay = 30000, int maxAttempts = 10, ILogger logger = null)
    {
      _reconnectFunc = reconnectFunc ?? throw new ArgumentNullException(nameof(reconnectFunc));
      _baseDelay = baseDelay;
      _maxDelay = maxDelay;
      _maxAttempts = maxAttempts;
      _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    ///   Resets the attempt counter. Call this after a successful connection.
    /// </summary>
    public void ResetAttempts() => Interlocked.Exchange(ref _attempt, 0);

    /// <summary>
    ///   Permanently stops the reconnection loop. No further attempts will be scheduled.
    /// </summary>
    public void Stop() => _stopped = true;

    /// <summary>
    ///   Gets the current number of reconnection attempts since the last successful connection.
    ///   Zero means no reconnection is in progress.
    /// </summary>
    public int CurrentAttempts => Volatile.Read(ref _attempt);

    /// <summary>
    ///   Returns true when the handler has exhausted its retry budget and will no longer
    ///   attempt to reconnect. Always false when <c>maxAttempts</c> is 0 (unlimited retries).
    /// </summary>
    public bool HasExhaustedAttempts => _maxAttempts > 0 && Volatile.Read(ref _attempt) >= _maxAttempts;

    /// <summary>
    ///   Schedules the first reconnection attempt when the channel becomes inactive.
    /// </summary>
    public override void ChannelInactive(IChannelHandlerContext context)
    {
      base.ChannelInactive(context);
      ScheduleReconnect();
    }

    /// <summary>
    ///   Schedules a reconnection attempt with exponential backoff. On failure the handler
    ///   re-schedules itself using <see cref="Task.Delay"/> so the loop survives even when
    ///   no DotNetty event loop is available (e.g., ConnectAsync threw before a channel was
    ///   created).
    /// </summary>
    private void ScheduleReconnect()
    {
      if (_stopped) return;

      var attempt = Volatile.Read(ref _attempt);
      if (_maxAttempts > 0 && attempt >= _maxAttempts)
      {
        _logger.LogError("Max reconnection attempts ({MaxAttempts}) reached. Giving up", _maxAttempts);
        return;
      }

      var delay = CalculateDelay(attempt);
      var currentAttempt = Interlocked.Increment(ref _attempt);

      _logger.LogInformation("Scheduling reconnect attempt {Attempt} in {Delay}ms",
        currentAttempt, delay);

      _ = RunReconnectAsync(delay, currentAttempt);
    }

    private async Task RunReconnectAsync(int delayMs, int attemptNumber)
    {
      try
      {
        await Task.Delay(delayMs);

        if (_stopped) return;

        await _reconnectFunc();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", attemptNumber);

        // The reconnect delegate failed (e.g., host unreachable). No new channel was
        // created, so no new ChannelInactive will fire. Self-schedule the next attempt
        // so the retry loop stays alive.
        ScheduleReconnect();
      }
    }

    /// <summary>
    ///   Calculates delay with exponential backoff and jitter.
    /// </summary>
    internal int CalculateDelay(int attempt)
    {
      var exponentialDelay = (long)_baseDelay * (1L << Math.Min(attempt, 20));
      var cappedDelay = (int)Math.Min(exponentialDelay, _maxDelay);

      var jitter = Random.Shared.Next(0, Math.Max(1, cappedDelay / 4));
      return cappedDelay + jitter;
    }
  }
}
