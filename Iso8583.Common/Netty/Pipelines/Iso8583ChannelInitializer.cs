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

using System.Security.Cryptography.X509Certificates;
using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
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
    /// <param name="reconnectHandler">optional reconnect handler for client connections</param>
    /// <param name="connectionTracker">optional connection tracker for server connections</param>
    /// <param name="metrics">optional metrics provider</param>
    public Iso8583ChannelInitializer(TC configuration, IPipelineConfigurator<TC> configurator,
      MultithreadEventLoopGroup workerGroup, IMessageFactory<IsoMessage> messageFactory,
      IChannelHandler channelHandler, IChannelHandler reconnectHandler = null,
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
      _isClient = reconnectHandler != null;
    }

    protected IMessageFactory<IsoMessage> MessageFactory { get; }

    private IsoMessageEncoder CreateIsoMessageEncoder(TC configuration) =>
      new(configuration.FrameLengthFieldLength, configuration.EncodeFrameLengthAsString, _metrics);

    private IsoMessageDecoder CreateIsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory) =>
      new(messageFactory, _metrics);

    private IChannelHandler CreateParseExceptionHandler() => new ParseExceptionHandler(MessageFactory, true);

    private static IChannelHandler CreateLoggingHandler(TC configuration) =>
      new IsoMessageLoggingHandler(LogLevel.DEBUG, configuration.LogSensitiveData,
        configuration.LogFieldDescription, configuration.SensitiveDataFields);

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

    protected override void InitChannel(ISocketChannel channel)
    {
      var pipeline = channel.Pipeline;

      // Add connection tracker for server connections (before everything else)
      if (_connectionTracker != null)
        pipeline.AddLast("connectionTracker", _connectionTracker);

      // Add TLS handler if SSL is configured
      var ssl = _configuration.Ssl;
      if (ssl is { Enabled: true })
      {
        if (_isClient)
        {
          var targetHost = ssl.TargetHost ?? channel.RemoteAddress?.ToString() ?? "localhost";
          if (ssl.MutualTls && !string.IsNullOrEmpty(ssl.CertificatePath))
          {
            var clientCert = LoadCertificate(ssl.CertificatePath, ssl.CertificatePassword);
            var clientSettings = new ClientTlsSettings(targetHost, [clientCert]);
            pipeline.AddLast("tls", new TlsHandler(clientSettings));
          }
          else
          {
            pipeline.AddLast("tls", new TlsHandler(new ClientTlsSettings(targetHost)));
          }
        }
        else
        {
          var serverCert = LoadCertificate(ssl.CertificatePath, ssl.CertificatePassword);
          pipeline.AddLast("tls", new TlsHandler(new ServerTlsSettings(serverCert, ssl.MutualTls)));
        }
      }

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
      pipeline.AddLast(_workerGroup, "messageHandler", _channelHandler);

      // Add reconnect handler for client connections
      if (_reconnectHandler != null)
        pipeline.AddLast("reconnect", _reconnectHandler);

      _configurator?.ConfigurePipeline(pipeline, _configuration);
    }

    private static X509Certificate2 LoadCertificate(string path, string password)
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

