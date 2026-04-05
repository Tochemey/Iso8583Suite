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

namespace Iso8583.Server.HealthChecks
{
  /// <summary>
  ///   ASP.NET Core health check for an <see cref="Iso8583Server{T}"/>.
  ///   Reports <see cref="HealthStatus.Healthy"/> when the server is listening and
  ///   <see cref="HealthStatus.Unhealthy"/> when it is not. The active connection count
  ///   is included in the result data.
  /// </summary>
  /// <typeparam name="T">The ISO message type.</typeparam>
  public sealed class Iso8583ServerHealthCheck<T> : IHealthCheck where T : IsoMessage
  {
    private readonly Iso8583Server<T> _server;

    /// <summary>
    ///   Creates a new instance of <see cref="Iso8583ServerHealthCheck{T}"/>.
    /// </summary>
    /// <param name="server">The ISO 8583 server to report on.</param>
    public Iso8583ServerHealthCheck(Iso8583Server<T> server)
    {
      _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
      CancellationToken cancellationToken = default)
    {
      var listening = _server.IsStarted();
      var activeConnections = _server.ActiveConnectionCount;

      var data = new Dictionary<string, object>
      {
        ["listening"] = listening,
        ["activeConnections"] = activeConnections
      };

      return Task.FromResult(listening
        ? HealthCheckResult.Healthy($"ISO 8583 server is listening ({activeConnections} active connections)", data)
        : HealthCheckResult.Unhealthy("ISO 8583 server is not listening", data: data));
    }
  }
}
