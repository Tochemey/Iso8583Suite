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
  ///   Strategy for selecting a connection from a pool.
  /// </summary>
  public interface ILoadBalancer
  {
    /// <summary>
    ///   Selects the next connection index from the currently active connections.
    /// </summary>
    /// <param name="activeConnections">Indices of currently connected (healthy) connections.</param>
    /// <returns>The selected connection index (one of the values in <paramref name="activeConnections"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="activeConnections"/> is empty.</exception>
    int Select(ReadOnlySpan<int> activeConnections);
  }
}
