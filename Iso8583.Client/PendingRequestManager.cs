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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   Manages pending request/response correlation using STAN (field 11) and message type.
  ///   Provides a <see cref="IIsoMessageListener{T}"/> that matches inbound responses to pending requests.
  /// </summary>
  /// <typeparam name="T">The IsoMessage type</typeparam>
  internal class PendingRequestManager<T> : IIsoMessageListener<T> where T : IsoMessage
  {
    private readonly ConcurrentDictionary<string, TaskCompletionSource<T>> _pending = new();

    /// <summary>
    ///   Registers a pending request and returns a task that completes when the matching response arrives.
    /// </summary>
    /// <param name="request">the outbound request message</param>
    /// <param name="timeout">how long to wait for a response</param>
    /// <param name="cancellationToken">optional cancellation token</param>
    /// <returns>the response message</returns>
    public async Task<T> RegisterPending(T request, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
      var key = BuildCorrelationKey(request);
      var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

      if (!_pending.TryAdd(key, tcs))
        throw new InvalidOperationException(
          $"A pending request with the same correlation key already exists: {key}");

      // Register timeout and cancellation
      using var timeoutCts = new CancellationTokenSource(timeout);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

      await using var registration = linkedCts.Token.Register(() =>
      {
        if (_pending.TryRemove(key, out var removed))
        {
          if (cancellationToken.IsCancellationRequested)
            removed.TrySetCanceled(cancellationToken);
          else
            removed.TrySetException(new TimeoutException(
              $"No response received for {key} within {timeout.TotalMilliseconds}ms"));
        }
      });

      return await tcs.Task;
    }

    /// <summary>
    ///   Cancels all pending requests (e.g., on disconnect).
    /// </summary>
    public void CancelAll()
    {
      foreach (var kvp in _pending)
      {
        if (_pending.TryRemove(kvp.Key, out var tcs))
          tcs.TrySetCanceled();
      }
    }

    /// <inheritdoc />
    public bool CanHandleMessage(T isoMessage)
    {
      var key = BuildResponseCorrelationKey(isoMessage);
      return _pending.ContainsKey(key);
    }

    /// <inheritdoc />
    public Task<bool> HandleMessage(IChannelHandlerContext context, T isoMessage)
    {
      var key = BuildResponseCorrelationKey(isoMessage);
      if (_pending.TryRemove(key, out var tcs))
      {
        tcs.TrySetResult(isoMessage);
        return Task.FromResult(false); // stop further processing for this correlated response
      }

      return Task.FromResult(true); // not ours, continue chain
    }

    /// <summary>
    ///   Builds a correlation key from a request message.
    ///   Key format: "{MTI}:{STAN}" where MTI is the request type (e.g., "1100")
    ///   and STAN is field 11.
    /// </summary>
    private static string BuildCorrelationKey(T message)
    {
      var stan = GetStan(message);
      return $"{message.Type:X4}:{stan}";
    }

    /// <summary>
    ///   Builds a correlation key from a response message.
    ///   Maps the response MTI back to the request MTI (e.g., 1110 -> 1100)
    ///   by clearing the function digit (position 3).
    /// </summary>
    private static string BuildResponseCorrelationKey(T message)
    {
      // Response MTI has function digit set (e.g., 1110 for request 1100).
      // The request MTI = response MTI with the tens digit zeroed out.
      // In ISO 8583, response type = request type + 0x0010
      var requestType = message.Type - 0x0010;
      var stan = GetStan(message);
      return $"{requestType:X4}:{stan}";
    }

    private static string GetStan(T message)
    {
      var field11 = message.GetField(11);
      if (field11 == null)
        throw new InvalidOperationException("Message has no STAN (field 11) for correlation");
      return field11.Value?.ToString() ?? "";
    }
  }
}
