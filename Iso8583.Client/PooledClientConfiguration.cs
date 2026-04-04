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
  ///   Configuration for <see cref="PooledIso8583Client{T}"/>.
  /// </summary>
  public class PooledClientConfiguration
  {
    /// <summary>
    ///   The number of connections to maintain in the pool. Default is 4.
    /// </summary>
    public int PoolSize { get; set; } = 4;

    /// <summary>
    ///   Interval between health checks that verify connection liveness and replace dead connections.
    ///   Default is 10 seconds.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    ///   The client configuration applied to each connection in the pool.
    /// </summary>
    public ClientConfiguration ClientConfiguration { get; set; } = new();

    /// <summary>
    ///   Validates the configuration values.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when any value is invalid.</exception>
    public void Validate()
    {
      if (PoolSize <= 0)
        throw new ArgumentException($"{nameof(PoolSize)} must be > 0, got {PoolSize}");
      if (HealthCheckInterval <= TimeSpan.Zero)
        throw new ArgumentException($"{nameof(HealthCheckInterval)} must be > 0");

      ClientConfiguration.Validate();
    }
  }
}
