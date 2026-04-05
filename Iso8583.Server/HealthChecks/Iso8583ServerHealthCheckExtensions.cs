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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCore8583;

namespace Iso8583.Server.HealthChecks
{
  /// <summary>
  ///   Extension methods for registering <see cref="Iso8583ServerHealthCheck{T}"/>
  ///   with <see cref="IHealthChecksBuilder"/>.
  /// </summary>
  public static class Iso8583ServerHealthCheckExtensions
  {
    /// <summary>
    ///   Registers an <see cref="Iso8583ServerHealthCheck{T}"/> that resolves its
    ///   <see cref="Iso8583Server{T}"/> from the service provider.
    /// </summary>
    /// <typeparam name="T">The ISO message type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "iso8583-server".</param>
    /// <param name="failureStatus">
    ///   The failure status used when the check reports unhealthy. Defaults to
    ///   <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags to associate with the check.</param>
    public static IHealthChecksBuilder AddIso8583ServerHealthCheck<T>(
      this IHealthChecksBuilder builder,
      string name = "iso8583-server",
      HealthStatus? failureStatus = null,
      IEnumerable<string> tags = null) where T : IsoMessage
    {
      if (builder is null) throw new ArgumentNullException(nameof(builder));

      return builder.Add(new HealthCheckRegistration(
        name,
        sp => new Iso8583ServerHealthCheck<T>(sp.GetRequiredService<Iso8583Server<T>>()),
        failureStatus,
        tags));
    }

    /// <summary>
    ///   Registers an <see cref="Iso8583ServerHealthCheck{T}"/> for an explicit server instance.
    /// </summary>
    /// <typeparam name="T">The ISO message type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="server">The server instance to monitor.</param>
    /// <param name="name">The health check name. Defaults to "iso8583-server".</param>
    /// <param name="failureStatus">
    ///   The failure status used when the check reports unhealthy. Defaults to
    ///   <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags to associate with the check.</param>
    public static IHealthChecksBuilder AddIso8583ServerHealthCheck<T>(
      this IHealthChecksBuilder builder,
      Iso8583Server<T> server,
      string name = "iso8583-server",
      HealthStatus? failureStatus = null,
      IEnumerable<string> tags = null) where T : IsoMessage
    {
      if (builder is null) throw new ArgumentNullException(nameof(builder));
      if (server is null) throw new ArgumentNullException(nameof(server));

      return builder.Add(new HealthCheckRegistration(
        name,
        _ => new Iso8583ServerHealthCheck<T>(server),
        failureStatus,
        tags));
    }
  }
}
