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

namespace Iso8583.Client
{
  /// <summary>
  ///   Selects the connection with the fewest pending requests.
  ///   Requires a callback to retrieve the pending count per connection index.
  /// </summary>
  public sealed class LeastConnectionsLoadBalancer : ILoadBalancer
  {
    private readonly Func<int, int> _pendingCountProvider;

    /// <summary>
    ///   Creates a new instance of <see cref="LeastConnectionsLoadBalancer"/>.
    /// </summary>
    /// <param name="pendingCountProvider">
    ///   A function that returns the number of in-flight requests for a given connection index.
    /// </param>
    public LeastConnectionsLoadBalancer(Func<int, int> pendingCountProvider)
    {
      _pendingCountProvider = pendingCountProvider
                              ?? throw new ArgumentNullException(nameof(pendingCountProvider));
    }

    /// <inheritdoc />
    public int Select(ReadOnlySpan<int> activeConnections)
    {
      if (activeConnections.Length == 0)
        throw new InvalidOperationException("No active connections available");

      var bestIndex = activeConnections[0];
      var bestCount = _pendingCountProvider(bestIndex);

      for (var i = 1; i < activeConnections.Length; i++)
      {
        var candidateIndex = activeConnections[i];
        var candidateCount = _pendingCountProvider(candidateIndex);
        if (candidateCount < bestCount)
        {
          bestIndex = candidateIndex;
          bestCount = candidateCount;
        }
      }

      return bestIndex;
    }
  }
}
