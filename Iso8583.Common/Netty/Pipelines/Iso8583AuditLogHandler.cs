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

using System.Collections.Generic;
using System.Diagnostics;
using DotNetty.Common.Concurrency;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using NetCore8583;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Channel handler that emits structured audit events for every inbound and outbound
  ///   <see cref="IsoMessage"/>. Events are published through a standard
  ///   <see cref="ILogger"/> under the <c>Iso8583.Audit</c> category so the host application's
  ///   logging pipeline (Serilog, NLog, OpenTelemetry, …) controls the transport and format.
  ///
  ///   <para>
  ///   Each event carries the following structured properties in the logger scope:
  ///   <list type="bullet">
  ///     <item><description><c>Iso8583.Direction</c> — <c>Inbound</c> or <c>Outbound</c>.</description></item>
  ///     <item><description><c>Iso8583.Mti</c> — the four-digit message type indicator.</description></item>
  ///     <item><description><c>Iso8583.Stan</c> — field 11 (system trace audit number), when present.</description></item>
  ///     <item><description><c>Iso8583.Rrn</c> — field 37 (retrieval reference number), when present.</description></item>
  ///     <item><description><c>Iso8583.CorrelationId</c> — derived from the MTI class and STAN, identical for a request and its matching response.</description></item>
  ///     <item><description><c>Iso8583.RemoteEndpoint</c> — the remote address of the channel.</description></item>
  ///     <item><description><c>Iso8583.DurationMs</c> — on response events, the elapsed milliseconds since the matching request.</description></item>
  ///     <item><description><c>Iso8583.Fields</c> — optional masked dictionary of the present fields; disabled by default.</description></item>
  ///   </list>
  ///   </para>
  ///
  ///   <para>
  ///   The handler is stateful per channel (tracks pending request timestamps for duration
  ///   correlation) and therefore not sharable; a new instance is installed per pipeline.
  ///   </para>
  /// </summary>
  public sealed class Iso8583AuditLogHandler : ChannelHandlerAdapter
  {
    /// <summary>
    ///   Conventional logger category under which audit events are emitted.
    /// </summary>
    public const string AuditLogCategory = "Iso8583.Audit";

    private readonly ILogger _logger;
    private readonly bool _includeFields;
    private readonly int[] _maskedFields;
    private readonly Dictionary<string, long> _pendingRequests = new();
    private string _remoteEndpoint;

    /// <summary>
    ///   Creates a new audit log handler.
    /// </summary>
    /// <param name="logger">logger used to emit structured audit events</param>
    /// <param name="includeFields">when <c>true</c>, a masked dictionary of every present field is attached to each event</param>
    /// <param name="maskedFields">optional override of the default masked field list</param>
    public Iso8583AuditLogHandler(ILogger logger, bool includeFields = false, int[] maskedFields = null)
    {
      _logger = logger;
      _includeFields = includeFields;
      _maskedFields = SensitiveDataMasker.NormalizeMaskedFields(maskedFields);
    }

    /// <summary>
    ///   The handler is stateful (pending-request map) and therefore not sharable.
    /// </summary>
    public override bool IsSharable => false;

    /// <inheritdoc />
    public override void ChannelActive(IChannelHandlerContext context)
    {
      _remoteEndpoint = context.Channel.RemoteAddress?.ToString();
      context.FireChannelActive();
    }

    /// <inheritdoc />
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
      if (message is IsoMessage iso)
        EmitAudit(iso, "Inbound", context);
      context.FireChannelRead(message);
    }

    /// <inheritdoc />
    public override void Write(IChannelHandlerContext context, object message, IPromise promise)
    {
      if (message is IsoMessage iso)
        EmitAudit(iso, "Outbound", context);
      context.WriteAsync(message, promise);
    }

    private void EmitAudit(IsoMessage message, string direction, IChannelHandlerContext context)
    {
      if (!_logger.IsEnabled(LogLevel.Information)) return;

      var mti = message.Type.ToString("X4");
      var stan = message.HasField(11) ? message.GetField(11)?.ToString() : null;
      var rrn = message.HasField(37) ? message.GetField(37)?.ToString() : null;
      var correlationId = BuildCorrelationId(message.Type, stan);
      var remote = _remoteEndpoint ?? context.Channel.RemoteAddress?.ToString();

      double? durationMs = null;
      if (stan != null)
      {
        if (IsRequest(message.Type))
        {
          _pendingRequests[stan] = Stopwatch.GetTimestamp();
        }
        else if (_pendingRequests.TryGetValue(stan, out var start))
        {
          durationMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
          _pendingRequests.Remove(stan);
        }
      }

      var properties = new Dictionary<string, object>
      {
        ["Iso8583.Direction"] = direction,
        ["Iso8583.Mti"] = mti,
        ["Iso8583.CorrelationId"] = correlationId
      };
      if (stan != null) properties["Iso8583.Stan"] = stan;
      if (rrn != null) properties["Iso8583.Rrn"] = rrn;
      if (remote != null) properties["Iso8583.RemoteEndpoint"] = remote;
      if (durationMs.HasValue) properties["Iso8583.DurationMs"] = durationMs.Value;
      if (_includeFields)
        properties["Iso8583.Fields"] = SensitiveDataMasker.BuildMaskedFieldMap(message, _maskedFields);

      using (_logger.BeginScope(properties))
      {
        if (durationMs.HasValue)
          _logger.LogInformation(
            "iso8583 audit {Direction} mti={Mti} stan={Stan} rrn={Rrn} duration={DurationMs:F3}ms",
            direction, mti, stan, rrn, durationMs.Value);
        else
          _logger.LogInformation(
            "iso8583 audit {Direction} mti={Mti} stan={Stan} rrn={Rrn}",
            direction, mti, stan, rrn);
      }
    }

    /// <summary>
    ///   Builds a correlation id from the MTI class byte and the STAN. A request and its
    ///   matching response share the same class byte, so both sides of the exchange emit
    ///   the same correlation id.
    /// </summary>
    private static string BuildCorrelationId(int mti, string stan)
    {
      var classByte = (mti >> 8) & 0xFF;
      return stan != null ? $"{classByte:X2}-{stan}" : $"{classByte:X2}";
    }

    /// <summary>
    ///   Determines whether an MTI represents a request (or advice) rather than a response.
    ///   ISO 8583 message-function digits: even (0, 2, 4, 6, 8) are requests/advices/notifications,
    ///   odd (1, 3, 5, 7, 9) are responses.
    /// </summary>
    private static bool IsRequest(int mti) => ((mti >> 4) & 0xF) % 2 == 0;
  }
}
