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
using System.Security.Cryptography.X509Certificates;
using DotNetty.Codecs;
using DotNetty.Handlers.Timeout;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Metrics;
using Iso8583.Common.Netty.Codecs;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Iso8583 channel pipeline initializer. Configures codecs, handlers, and optional TLS.
  /// </summary>
  /// <typeparam name="TC"></typeparam>
  public class Iso8583ChannelInitializer<TC> : ChannelInitializer<ISocketChannel>
    where TC : ConnectorConfiguration
  {
    private readonly IChannelHandler _channelHandler;
    private readonly TC _configuration;
    private readonly IPipelineConfigurator<TC> _configurator;
    private readonly MultithreadEventLoopGroup _workerGroup;
    private readonly IChannelHandler _reconnectHandler;
    private readonly IChannelHandler _connectionTracker;
    private readonly IIso8583Metrics _metrics;
    private readonly bool _isClient;

    /// <summary>
    ///   creates a new instance ChannelInitializer
    /// </summary>
    /// <param name="configuration">the configuration</param>
    /// <param name="configurator">the connector configurator</param>
    /// <param name="workerGroup">the worker group</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="channelHandler">the channel handler</param>
    /// <param name="isClient">
    ///   True when this initializer is used on the client side, false on the server side.
    ///   Controls TLS handler type (client vs server) and client-only pipeline handlers.
    /// </param>
    /// <param name="reconnectHandler">optional reconnect handler for client connections</param>
    /// <param name="connectionTracker">optional connection tracker for server connections</param>
    /// <param name="metrics">optional metrics provider</param>
    public Iso8583ChannelInitializer(TC configuration, IPipelineConfigurator<TC> configurator,
      MultithreadEventLoopGroup workerGroup, IMessageFactory<IsoMessage> messageFactory,
      IChannelHandler channelHandler, bool isClient, IChannelHandler reconnectHandler = null,
      IChannelHandler connectionTracker = null, IIso8583Metrics metrics = null)
    {
      _configuration = configuration;
      _configurator = configurator;
      _workerGroup = workerGroup;
      MessageFactory = messageFactory;
      _channelHandler = channelHandler;
      _reconnectHandler = reconnectHandler;
      _connectionTracker = connectionTracker;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
      _isClient = isClient;
    }

    /// <summary>
    ///   The message factory used by the pipeline to create decoders and echo/error handlers.
    /// </summary>
    protected IMessageFactory<IsoMessage> MessageFactory { get; }

    /// <summary>
    ///   Creates the encoder that serializes <see cref="IsoMessage"/> instances into wire bytes.
    /// </summary>
    private IsoMessageEncoder CreateIsoMessageEncoder(TC configuration) =>
      new(configuration.FrameLengthFieldLength, configuration.EncodeFrameLengthAsString, _metrics);

    /// <summary>
    ///   Creates the decoder that parses wire bytes into <see cref="IsoMessage"/> instances.
    /// </summary>
    private IsoMessageDecoder CreateIsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory) =>
      new(messageFactory, _metrics);

    /// <summary>
    ///   Creates a handler that sends an administrative error response when a parse exception occurs.
    /// </summary>
    private IChannelHandler CreateParseExceptionHandler() => new ParseExceptionHandler(MessageFactory, true);

    /// <summary>
    ///   Creates the ISO message logging handler with the configured sensitivity and field description settings.
    /// </summary>
    private static IChannelHandler CreateLoggingHandler(TC configuration) =>
      new IsoMessageLoggingHandler(configuration.LogLevel, configuration.LogSensitiveData,
        configuration.LogFieldDescription, configuration.SensitiveDataFields);

    /// <summary>
    ///   Creates the frame decoder that extracts individual ISO 8583 message frames from the TCP byte stream.
    ///   Uses <see cref="StringLengthFieldBasedFrameDecoder"/> when the length header is ASCII-encoded,
    ///   or <see cref="LengthFieldBasedFrameDecoder"/> for binary length headers.
    /// </summary>
    private static IChannelHandler CreateLengthFieldBasedFrameDecoder(TC configuration)
    {
      var lengthFieldLength = configuration.FrameLengthFieldLength;
      return configuration.EncodeFrameLengthAsString
        ? new StringLengthFieldBasedFrameDecoder(
          configuration.MaxFrameLength,
          configuration.FrameLengthFieldOffset,
          lengthFieldLength,
          configuration.FrameLengthFieldAdjust,
          lengthFieldLength
        )
        : new LengthFieldBasedFrameDecoder(
          configuration.MaxFrameLength,
          configuration.FrameLengthFieldOffset,
          lengthFieldLength,
          configuration.FrameLengthFieldAdjust,
          lengthFieldLength
        );
    }

    /// <summary>
    ///   Builds the full channel pipeline: TLS (optional), frame decoder, ISO codec, logging,
    ///   error handling, idle detection, message handler, and reconnection (client only).
    /// </summary>
    protected override void InitChannel(ISocketChannel channel)
    {
      var pipeline = channel.Pipeline;

      // Add connection tracker for server connections (before everything else)
      if (_connectionTracker != null)
        pipeline.AddLast("connectionTracker", _connectionTracker);

      // Add TLS handler if SSL is configured
      TryAddTlsHandler(pipeline, _configuration.Ssl, _isClient,
        _isClient ? channel.RemoteAddress?.ToString() : null);

      var isoMessageEncoder = CreateIsoMessageEncoder(_configuration);
      var loggingHandler = CreateLoggingHandler(_configuration);
      var parseExceptionHandler = CreateParseExceptionHandler();

      pipeline.AddLast(
        "lengthFieldFrameDecoder",
        CreateLengthFieldBasedFrameDecoder(_configuration)
      );
      pipeline.AddLast("iso8583Decoder", CreateIsoMessageDecoder(MessageFactory));
      pipeline.AddLast("iso8583Encoder", isoMessageEncoder);
      if (_configuration.AddLoggingHandler) pipeline.AddLast(_workerGroup, "logging", loggingHandler);
      if (_configuration.ReplyOnError) pipeline.AddLast(_workerGroup, "replyOnError", parseExceptionHandler);
      pipeline.AddLast("idleState", new IdleStateHandler(0, 0, _configuration.IdleTimeout));
      pipeline.AddLast("idleEventHandler", new IdleEventHandler(MessageFactory));
      // Validation is always installed. When no validator is configured it is a pass-through;
      // when a validator is attached, invalid messages fail outbound writes and fire exception
      // events on inbound reads so existing error handlers can react.
      pipeline.AddLast("messageValidation", new MessageValidationHandler(_configuration.MessageValidator));
      if (_configuration.EnableAuditLog)
      {
        var auditLogger = _configuration.AuditLogger
                          ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        pipeline.AddLast("auditLog",
          new Iso8583AuditLogHandler(auditLogger, _configuration.AuditLogIncludeFields,
            _configuration.SensitiveDataFields));
      }
      pipeline.AddLast(_workerGroup, "messageHandler", _channelHandler);

      // Add reconnect handler for client connections
      if (_reconnectHandler != null)
        pipeline.AddLast("reconnect", _reconnectHandler);

      _configurator?.ConfigurePipeline(pipeline, _configuration);
    }

    /// <summary>
    ///   Installs a <see cref="TlsHandler"/> at the head of the given pipeline when SSL is
    ///   enabled. Extracted from <see cref="InitChannel"/> so the TLS wiring can be exercised
    ///   by unit tests with an <c>EmbeddedChannel</c> pipeline without requiring a real
    ///   TCP handshake.
    /// </summary>
    internal static void TryAddTlsHandler(IChannelPipeline pipeline, SslConfiguration ssl, bool isClient,
      string clientRemoteAddress)
    {
      if (ssl is not { Enabled: true }) return;

      if (isClient)
      {
        var targetHost = ssl.TargetHost ?? clientRemoteAddress ?? "localhost";
        ClientTlsSettings clientSettings;
        var clientCert = ResolveCertificate(ssl);
        if (ssl.MutualTls && clientCert != null)
        {
          clientSettings = new ClientTlsSettings(targetHost, [clientCert]);
        }
        else
        {
          clientSettings = new ClientTlsSettings(targetHost);
        }

        if (ssl.AllowUntrustedCertificates)
        {
          clientSettings.AllowUnstrustedCertificate = true;
          clientSettings.AllowNameMismatchCertificate = true;
          clientSettings.AllowCertificateChainErrors = true;
        }

        pipeline.AddLast("tls", new TlsHandler(clientSettings));
      }
      else
      {
        var serverCert = ResolveCertificate(ssl)
                         ?? throw new InvalidOperationException(
                           "Server SSL is enabled but no certificate is configured. " +
                           "Set SslConfiguration.Certificate or SslConfiguration.CertificatePath.");
        pipeline.AddLast("tls", new TlsHandler(new ServerTlsSettings(serverCert, ssl.MutualTls)));
      }
    }

    /// <summary>
    ///   Resolves the certificate from an <see cref="SslConfiguration"/>, preferring an
    ///   already-loaded <see cref="SslConfiguration.Certificate"/> instance when present and
    ///   falling back to loading from <see cref="SslConfiguration.CertificatePath"/>.
    /// </summary>
    internal static X509Certificate2 ResolveCertificate(SslConfiguration ssl)
    {
      if (ssl.Certificate != null) return ssl.Certificate;
      if (string.IsNullOrEmpty(ssl.CertificatePath)) return null;
      return LoadCertificate(ssl.CertificatePath, ssl.CertificatePassword);
    }

    /// <summary>
    ///   Loads a PKCS#12 certificate from the given file path. Uses the platform-appropriate loader
    ///   (<c>X509CertificateLoader</c> on .NET 9+, constructor on older runtimes).
    /// </summary>
    internal static X509Certificate2 LoadCertificate(string path, string password)
    {
#if NET9_0_OR_GREATER
      return X509CertificateLoader.LoadPkcs12FromFile(path, password);
#else
#pragma warning disable SYSLIB0057
      return new X509Certificate2(path, password);
#pragma warning restore SYSLIB0057
#endif
    }
  }
}

