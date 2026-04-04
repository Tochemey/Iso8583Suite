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

namespace Iso8583.Client
{
  /// <summary>
  ///   Distributes requests across connections in a round-robin fashion.
  /// </summary>
  public sealed class RoundRobinLoadBalancer : ILoadBalancer
  {
    private int _counter;

    /// <inheritdoc />
    public int Select(ReadOnlySpan<int> activeConnections)
    {
      if (activeConnections.Length == 0)
        throw new InvalidOperationException("No active connections available");

      var index = Interlocked.Increment(ref _counter);
      // Mask the sign bit so modulo always yields a non-negative position, even after overflow.
      var position = (index & int.MaxValue) % activeConnections.Length;
      return activeConnections[position];
    }
  }
}
