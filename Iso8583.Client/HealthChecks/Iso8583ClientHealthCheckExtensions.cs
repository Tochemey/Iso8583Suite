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

namespace Iso8583.Client.HealthChecks
{
  /// <summary>
  ///   Extension methods for registering <see cref="Iso8583ClientHealthCheck{T}"/>
  ///   with <see cref="IHealthChecksBuilder"/>.
  /// </summary>
  public static class Iso8583ClientHealthCheckExtensions
  {
    /// <summary>
    ///   Registers an <see cref="Iso8583ClientHealthCheck{T}"/> that resolves its
    ///   <see cref="Iso8583Client{T}"/> from the service provider.
    /// </summary>
    /// <typeparam name="T">The ISO message type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to "iso8583-client".</param>
    /// <param name="failureStatus">
    ///   The failure status used when the check reports unhealthy. Defaults to
    ///   <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags to associate with the check.</param>
    public static IHealthChecksBuilder AddIso8583ClientHealthCheck<T>(
      this IHealthChecksBuilder builder,
      string name = "iso8583-client",
      HealthStatus? failureStatus = null,
      IEnumerable<string> tags = null) where T : IsoMessage
    {
      if (builder is null) throw new ArgumentNullException(nameof(builder));

      return builder.Add(new HealthCheckRegistration(
        name,
        sp => new Iso8583ClientHealthCheck<T>(sp.GetRequiredService<Iso8583Client<T>>()),
        failureStatus,
        tags));
    }

    /// <summary>
    ///   Registers an <see cref="Iso8583ClientHealthCheck{T}"/> for an explicit client instance.
    /// </summary>
    /// <typeparam name="T">The ISO message type.</typeparam>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="client">The client instance to monitor.</param>
    /// <param name="name">The health check name. Defaults to "iso8583-client".</param>
    /// <param name="failureStatus">
    ///   The failure status used when the check reports unhealthy. Defaults to
    ///   <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags to associate with the check.</param>
    public static IHealthChecksBuilder AddIso8583ClientHealthCheck<T>(
      this IHealthChecksBuilder builder,
      Iso8583Client<T> client,
      string name = "iso8583-client",
      HealthStatus? failureStatus = null,
      IEnumerable<string> tags = null) where T : IsoMessage
    {
      if (builder is null) throw new ArgumentNullException(nameof(builder));
      if (client is null) throw new ArgumentNullException(nameof(client));

      return builder.Add(new HealthCheckRegistration(
        name,
        _ => new Iso8583ClientHealthCheck<T>(client),
        failureStatus,
        tags));
    }
  }
}
