using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Codecs;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Server
{
    /// <summary>
    ///   Iso8583ServerChannelInitializer
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="B"></typeparam>
    /// <typeparam name="C"></typeparam>
    public class Iso8583ServerChannelInitializer<T, B, C> : ChannelInitializer<T> where T : ISocketChannel
    where C : ConnectorConfiguration
    where B : ServerBootstrap
  {
    private readonly IChannelHandler _channelHandler;
    private readonly C _configuration;
    private readonly IServerConnectorConfigurer<C, B> _configurer;
    private readonly MultithreadEventLoopGroup _workerGroup;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583ServerChannelInitializer{T,B,C}" />
    /// </summary>
    /// <param name="configuration">the configuration</param>
    /// <param name="configurer">the connector configurer</param>
    /// <param name="workerGroup">the worker group</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="channelHandler">the channel handler</param>
    public Iso8583ServerChannelInitializer(C configuration, IServerConnectorConfigurer<C, B> configurer,
      MultithreadEventLoopGroup workerGroup, IMessageFactory<IsoMessage> messageFactory,
      IChannelHandler channelHandler)
    {
      _configuration = configuration;
      _configurer = configurer;
      _workerGroup = workerGroup;
      MessageFactory = messageFactory;
      _channelHandler = channelHandler;
    }

    public IMessageFactory<IsoMessage> MessageFactory { get; }

    protected override void InitChannel(T channel)
    {
      var isoMessageEncoder = CreateIsoMessageEncoder(_configuration);
      var loggingHandler = CreateLoggingHandler(_configuration);
      var parseExceptionHandler = CreateParseExceptionHandler();

      var pipeline = channel.Pipeline;

      // set the channel pipeline
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
      pipeline.AddLast(_workerGroup, _channelHandler);
      _configurer?.ConfigurePipeline(pipeline, _configuration);
    }

    /// <summary>
    ///   creates the iso 8583 message encoder given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns>the message encoder <see cref="IsoMessageEncoder" /></returns>
    private IsoMessageEncoder CreateIsoMessageEncoder(C configuration)
    {
      return new(configuration.FrameLenghtFieldLength, configuration.EncodeFrameLengthAsString);
    }

    /// <summary>
    ///   creates the iso 8583 message decoder given the message factory
    /// </summary>
    /// <param name="messageFactory">the iso message factory</param>
    /// <returns>the message decoder <see cref="IsoMessageDecoder" /></returns>
    private IsoMessageDecoder CreateIsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory)
    {
      return new(messageFactory);
    }

    /// <summary>
    ///   creates the parsing exception handler
    /// </summary>
    /// <returns>the parsing exception handler <see cref="ParseExceptionHandler" /></returns>
    private IChannelHandler CreateParseExceptionHandler()
    {
      return new ParseExceptionHandler(MessageFactory, true);
    }

    /// <summary>
    ///   creates the logging handler given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns>the logging handler <see cref="IsoMessageLoggingHandler" /></returns>
    private IChannelHandler CreateLoggingHandler(C configuration)
    {
      return new IsoMessageLoggingHandler(LogLevel.DEBUG, configuration.LogSensitiveData,
        configuration.LogFieldDescription, configuration.SensitiveDataFields);
    }

    /// <summary>
    ///   creates the length field based frame decoder given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns></returns>
    private IChannelHandler CreateLengthFieldBasedFrameDecoder(C configuration)
    {
      var lengthFieldLength = configuration.FrameLenghtFieldLength;
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
  }
}