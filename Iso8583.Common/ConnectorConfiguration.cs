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
using Iso8583.Common.Netty.Pipelines;
using Iso8583.Common.Validation;
using DotNettyLogLevel = DotNetty.Handlers.Logging.LogLevel;

namespace Iso8583.Common
{
  /// <summary>
  ///   ConnectorConfiguration abstract class
  /// </summary>
  public abstract class ConnectorConfiguration
  {
    /// <summary>
    ///   Default read/write idle timeout in seconds (ping interval) = 30 sec.
    /// </summary>
    private const int DefaultIdleTimeout = 30;

    /// <summary>
    ///   Default (max message length) = 8192
    /// </summary>
    private const int DefaultMaxFrameLength = 8192;

    /// <summary>
    ///   Default (length of TCP Frame length) = 2
    /// </summary>
    private const int DefaultFrameLengthFieldLength = 2;

    /// <summary>
    ///   Default (compensation value to add to the value of the length field) = 0
    /// </summary>
    private const int DefaultFrameLengthFieldAdjust = 0;

    /// <summary>
    ///   Default  (the offset of the length field) = 0
    /// </summary>
    private const int DefaultFrameLengthFieldOffset = 0;

    private static readonly int[] DefaultSensitiveDataFields = IsoMessageLoggingHandler.DefaultMaskedFields;

    /// <summary>
    ///   default constructor
    /// </summary>
    protected ConnectorConfiguration()
    {
      EncodeFrameLengthAsString = false;
      AddLoggingHandler = false;
      AddEchoMessageListener = false;
      LogFieldDescription = true;
      LogSensitiveData = true;
      ReplyOnError = false;
      MaxFrameLength = DefaultMaxFrameLength;
      IdleTimeout = DefaultIdleTimeout;
      FrameLengthFieldLength = DefaultFrameLengthFieldLength;
      FrameLengthFieldOffset = DefaultFrameLengthFieldOffset;
      FrameLengthFieldAdjust = DefaultFrameLengthFieldAdjust;
      SensitiveDataFields = DefaultSensitiveDataFields;
      WorkerThreadCount = Math.Max(2, Environment.ProcessorCount * 2);
      LogLevel = DotNettyLogLevel.DEBUG;
      EnableAuditLog = false;
      AuditLogIncludeFields = false;
    }

    /// <summary>
    ///   Allows to add default echo message listener to the pipeline
    /// </summary>
    public bool AddEchoMessageListener { get; set; }

    /// <summary>
    ///   Maximum frame length
    /// </summary>
    public int MaxFrameLength { get; set; }

    /// <summary>
    ///   Set channel read/write idle timeout in seconds.
    ///   If no message was received/sent during specified time interval then `Echo` message will be sent
    /// </summary>
    public int IdleTimeout { get; set; }

    /// <summary>
    ///   Returns number of threads in worker event loop group.
    ///   Default value is Environment.ProcessorCount * 2 (minimum 2).
    ///   DotNetty event loops are non-blocking, so high multipliers are unnecessary.
    /// </summary>
    public int WorkerThreadCount { get; set; }

    /// <summary>
    ///   Whether to reply with administrative message in case of message syntax errors. Default value is false.
    /// </summary>
    public bool ReplyOnError { get; set; }

    /// <summary>
    ///   addLoggingHandler `true` if <see cref="IsoMessageLoggingHandler" /> should be added to the pipeline.
    /// </summary>
    public bool AddLoggingHandler { get; set; }

    /// <summary>
    ///   Should log sensitive data (unmasked) or not. Not recommended in production
    ///   `true` if sensitive information like PAN, CVV/CVV2, and Track2 should be printed to log.
    ///   Default value is true
    /// </summary>
    public bool LogSensitiveData { get; set; }

    /// <summary>
    ///   If <code>true</code> then the length header is to be encoded as a String, as opposed to the default binary
    /// </summary>
    public bool EncodeFrameLengthAsString { get; set; }

    /// <summary>
    ///   Array of sensitive fields
    /// </summary>
    public int[] SensitiveDataFields { get; set; }

    /// <summary>
    ///   `true` to print ISO field descriptions in the log
    /// </summary>
    public bool LogFieldDescription { get; set; }

    /// <summary>
    ///   length of TCP frame length field. Default value is 2
    /// </summary>
    public int FrameLengthFieldLength { get; set; }

    /// <summary>
    ///   The offset of the length field. Default value is 0
    /// </summary>
    public int FrameLengthFieldOffset { get; set; }

    /// <summary>
    ///   Returns the compensation value to add to the value of the length field.
    ///   Default value is 0.
    /// </summary>
    public int FrameLengthFieldAdjust { get; set; }

    /// <summary>
    ///   SSL/TLS configuration. Null or Enabled=false means no TLS.
    /// </summary>
    public SslConfiguration Ssl { get; set; }

    /// <summary>
    ///   Optional message validator applied to every inbound and outbound <see cref="NetCore8583.IsoMessage"/>.
    ///   When <c>null</c> (default), the validation handler is still installed in the pipeline but
    ///   acts as a pass-through. When set, any message that fails validation causes a
    ///   <see cref="MessageValidationException"/> to be raised on the pipeline: outbound writes fail
    ///   synchronously, inbound messages fire an exception event that existing error handlers can react to.
    /// </summary>
    public MessageValidator MessageValidator { get; set; }

    /// <summary>
    ///   Log level applied to the DotNetty <c>LoggingHandler</c> / <see cref="IsoMessageLoggingHandler"/>
    ///   installed by the pipeline when <see cref="AddLoggingHandler"/> is <c>true</c>, and to the
    ///   server's acceptor <c>LoggingHandler</c>. Defaults to <see cref="DotNettyLogLevel.DEBUG"/>.
    /// </summary>
    public DotNettyLogLevel LogLevel { get; set; }

    /// <summary>
    ///   When <c>true</c>, the structured audit log handler is inserted into the channel pipeline
    ///   and emits one event per inbound and outbound <see cref="NetCore8583.IsoMessage"/> through
    ///   <see cref="AuditLogger"/>. When <c>false</c> (default), the handler is not installed and
    ///   there is zero runtime cost.
    /// </summary>
    public bool EnableAuditLog { get; set; }

    /// <summary>
    ///   Logger used by the audit handler to publish structured events. Callers should supply a
    ///   logger created with category <see cref="Iso8583AuditLogHandler.AuditLogCategory"/> (for
    ///   example <c>loggerFactory.CreateLogger("Iso8583.Audit")</c>) so downstream sinks can
    ///   route audit events separately from diagnostic logs. When <see cref="EnableAuditLog"/>
    ///   is <c>true</c> and this property is <c>null</c>, the handler is still installed but
    ///   uses a no-op logger.
    /// </summary>
    public Microsoft.Extensions.Logging.ILogger AuditLogger { get; set; }

    /// <summary>
    ///   When <c>true</c>, the audit event carries a masked dictionary of every present ISO 8583
    ///   field under the <c>Iso8583.Fields</c> property. Disabled by default to keep log volume down.
    /// </summary>
    public bool AuditLogIncludeFields { get; set; }

    /// <summary>
    ///   Validates the configuration and throws <see cref="ArgumentException"/> for invalid values.
    /// </summary>
    public virtual void Validate()
    {
      if (MaxFrameLength <= 0)
        throw new ArgumentException($"{nameof(MaxFrameLength)} must be > 0, got {MaxFrameLength}");
      if (IdleTimeout < 0)
        throw new ArgumentException($"{nameof(IdleTimeout)} must be >= 0, got {IdleTimeout}");
      if (WorkerThreadCount < 1)
        throw new ArgumentException($"{nameof(WorkerThreadCount)} must be >= 1, got {WorkerThreadCount}");
      if (FrameLengthFieldLength is < 0 or > 4)
        throw new ArgumentException($"{nameof(FrameLengthFieldLength)} must be 0-4, got {FrameLengthFieldLength}");
      if (FrameLengthFieldOffset < 0)
        throw new ArgumentException($"{nameof(FrameLengthFieldOffset)} must be >= 0, got {FrameLengthFieldOffset}");
    }
  }
}
