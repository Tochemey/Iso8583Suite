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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCore8583;

namespace Iso8583.Client.HealthChecks
{
  /// <summary>
  ///   ASP.NET Core health check for an <see cref="Iso8583Client{T}"/>.
  ///   Reports <see cref="HealthStatus.Healthy"/> when the client is connected,
  ///   <see cref="HealthStatus.Degraded"/> when the client is actively reconnecting,
  ///   and <see cref="HealthStatus.Unhealthy"/> when the client is disconnected.
  /// </summary>
  /// <typeparam name="T">The ISO message type.</typeparam>
  public sealed class Iso8583ClientHealthCheck<T> : IHealthCheck where T : IsoMessage
  {
    private readonly Iso8583Client<T> _client;

    /// <summary>
    ///   Creates a new instance of <see cref="Iso8583ClientHealthCheck{T}"/>.
    /// </summary>
    /// <param name="client">The ISO 8583 client to report on.</param>
    public Iso8583ClientHealthCheck(Iso8583Client<T> client)
    {
      _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
      CancellationToken cancellationToken = default)
    {
      var data = new Dictionary<string, object>
      {
        ["connected"] = _client.IsConnected(),
        ["reconnecting"] = _client.IsReconnecting
      };

      if (_client.IsConnected())
        return Task.FromResult(HealthCheckResult.Healthy("ISO 8583 client is connected", data));

      if (_client.IsReconnecting)
        return Task.FromResult(HealthCheckResult.Degraded("ISO 8583 client is reconnecting", data: data));

      return Task.FromResult(HealthCheckResult.Unhealthy("ISO 8583 client is disconnected", data: data));
    }
  }
}
