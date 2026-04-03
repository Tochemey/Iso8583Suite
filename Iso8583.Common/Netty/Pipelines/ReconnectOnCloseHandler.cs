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
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Client-side handler that triggers automatic reconnection with exponential backoff
  ///   when the channel is closed unexpectedly.
  /// </summary>
  public class ReconnectOnCloseHandler : ChannelHandlerAdapter
  {
    private readonly Func<Task> _reconnectFunc;
    private readonly int _baseDelay;
    private readonly int _maxDelay;
    private readonly int _maxAttempts;
    private readonly ILogger _logger;
    private int _attempt;

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
    public void ResetAttempts() => _attempt = 0;

    /// <summary>
    ///   Schedules a reconnection attempt with exponential backoff when the channel becomes inactive.
    ///   Stops retrying after <see cref="_maxAttempts"/> (unless set to 0 for unlimited).
    /// </summary>
    public override void ChannelInactive(IChannelHandlerContext context)
    {
      base.ChannelInactive(context);

      if (_maxAttempts > 0 && _attempt >= _maxAttempts)
      {
        _logger.LogError("Max reconnection attempts ({MaxAttempts}) reached. Giving up", _maxAttempts);
        return;
      }

      var delay = CalculateDelay(_attempt);
      _attempt++;

      _logger.LogInformation("Channel disconnected. Scheduling reconnect attempt {Attempt} in {Delay}ms",
        _attempt, delay);

      // Schedule reconnect on the event loop
      context.Channel.EventLoop.Schedule(async () =>
      {
        try
        {
          await _reconnectFunc();
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", _attempt);
        }
      }, TimeSpan.FromMilliseconds(delay));
    }

    /// <summary>
    ///   Calculates delay with exponential backoff and jitter.
    /// </summary>
    private int CalculateDelay(int attempt)
    {
      // Exponential: baseDelay * 2^attempt, capped at maxDelay
      var exponentialDelay = (long)_baseDelay * (1L << Math.Min(attempt, 20));
      var cappedDelay = (int)Math.Min(exponentialDelay, _maxDelay);

      // Add jitter: random 0-25% on top
      var jitter = Random.Shared.Next(0, Math.Max(1, cappedDelay / 4));
      return cappedDelay + jitter;
    }
  }
}
