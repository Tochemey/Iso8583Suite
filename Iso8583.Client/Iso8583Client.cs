using System;
using System.Net;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   An instance of this class will help bootstrap an iso 8583 client
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class Iso8583Client<T> : ClientConnector<T, ClientConfiguration> where T : IsoMessage
  {
    /// <summary>
    ///   server port
    /// </summary>
    private readonly IPEndPoint _endPoint;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="endPoint"></param>
    /// <param name="configuration"></param>
    /// <param name="messageFactory"></param>
    public Iso8583Client(IPEndPoint endPoint, ClientConfiguration configuration, IMessageFactory<T> messageFactory) : base(
      messageFactory, configuration) =>
      _endPoint = endPoint;

    /// <summary>
    ///   create a new instance of <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="endPoint"></param>
    /// <param name="messageFactory"></param>
    public Iso8583Client(IPEndPoint endPoint, IMessageFactory<T> messageFactory) : base(
      messageFactory, new ClientConfiguration()) =>
      _endPoint = endPoint;

    protected override Bootstrap CreateBootstrap()
    {
      var bootstrap = new Bootstrap();
      bootstrap.Group(BossEventLoopGroup);
      bootstrap.Channel<TcpSocketChannel>();
      bootstrap.RemoteAddress(_endPoint);
      bootstrap.Handler(new Iso8583ChannelInitializer<ISocketChannel, ClientConfiguration>(
        Configuration, ConnectorConfigurator, WorkerEventLoopGroup,
        MessageFactory as IMessageFactory<IsoMessage>, MessageHandler
      ));
      ConfigureBootstrap(bootstrap);
      bootstrap.Validate();

      return bootstrap;
    }
  }
}