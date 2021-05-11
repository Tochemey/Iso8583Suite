using DotNetty.Codecs;
using DotNetty.Handlers.Logging;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Codecs;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Iso8583ServerChannelInitializer
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <typeparam name="TC"></typeparam>
  public class Iso8583ChannelInitializer<T, TC> : ChannelInitializer<T> where T : ISocketChannel
    where TC : ConnectorConfiguration
  {
    private readonly IChannelHandler _channelHandler;
    private readonly TC _configuration;

    private readonly IPipelineConfigurator<TC> _configurator;
    private readonly MultithreadEventLoopGroup _workerGroup;

    /// <summary>
    ///   creates a new instance ChannelInitializer
    /// </summary>
    /// <param name="configuration">the configuration</param>
    /// <param name="configurator">the connector configurator</param>
    /// <param name="workerGroup">the worker group</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="channelHandler">the channel handler</param>
    public Iso8583ChannelInitializer(TC configuration, IPipelineConfigurator<TC> configurator,
      MultithreadEventLoopGroup workerGroup, IMessageFactory<IsoMessage> messageFactory,
      IChannelHandler channelHandler)
    {
      _configuration = configuration;
      _configurator = configurator;
      _workerGroup = workerGroup;
      MessageFactory = messageFactory;
      _channelHandler = channelHandler;
    }

    protected IMessageFactory<IsoMessage> MessageFactory { get; }

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
      _configurator?.ConfigurePipeline(pipeline, _configuration);
    }

    /// <summary>
    ///   creates the iso 8583 message encoder given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns>the message encoder <see cref="IsoMessageEncoder" /></returns>
    private IsoMessageEncoder CreateIsoMessageEncoder(TC configuration) =>
      new(configuration.FrameLenghtFieldLength, configuration.EncodeFrameLengthAsString);

    /// <summary>
    ///   creates the iso 8583 message decoder given the message factory
    /// </summary>
    /// <param name="messageFactory">the iso message factory</param>
    /// <returns>the message decoder <see cref="IsoMessageDecoder" /></returns>
    private IsoMessageDecoder CreateIsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory) =>
      new(messageFactory);

    /// <summary>
    ///   creates the parsing exception handler
    /// </summary>
    /// <returns>the parsing exception handler <see cref="ParseExceptionHandler" /></returns>
    private IChannelHandler CreateParseExceptionHandler() => new ParseExceptionHandler(MessageFactory, true);

    /// <summary>
    ///   creates the logging handler given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns>the logging handler <see cref="IsoMessageLoggingHandler" /></returns>
    private static IChannelHandler CreateLoggingHandler(TC configuration) =>
      new IsoMessageLoggingHandler(LogLevel.DEBUG, configuration.LogSensitiveData,
        configuration.LogFieldDescription, configuration.SensitiveDataFields);

    /// <summary>
    ///   creates the length field based frame decoder given the connector configuration
    /// </summary>
    /// <param name="configuration">the connector configuration</param>
    /// <returns></returns>
    private static IChannelHandler CreateLengthFieldBasedFrameDecoder(TC configuration)
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