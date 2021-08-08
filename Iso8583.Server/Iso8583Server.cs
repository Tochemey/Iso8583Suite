using System.Net;
using System.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Server
{
  /// <summary>
  ///   An instance of this class will help bootstrap an iso 8583 server
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class Iso8583Server<T> : ServerConnector<T, ServerConfiguration> where T : IsoMessage
  {
    /// <summary>
    ///   server port
    /// </summary>
    private readonly int _port;

    private readonly IPEndPoint _socketAddress;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Server{T}" />
    /// </summary>
    /// <param name="port">the server port</param>
    /// <param name="configuration">the server configuration</param>
    /// <param name="messageFactory">the message factory</param>
    public Iso8583Server(int port, ServerConfiguration configuration, IMessageFactory<T> messageFactory) : base(
      messageFactory, configuration)
    {
      _port = port;
      _socketAddress = new IPEndPoint(IPAddress.Any,
        port);
    }

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Server{T}" />
    /// </summary>
    /// <param name="port">the server port</param>
    /// <param name="messageFactory">the message factory</param>
    public Iso8583Server(int port, IMessageFactory<T> messageFactory) : base(
      messageFactory, new ServerConfiguration())
    {
      _port = port;
      _socketAddress = new IPEndPoint(IPAddress.Any,
        port);
    }


    protected override ServerBootstrap CreateBootstrap()
    {
      var boostrap = new ServerBootstrap();
      boostrap.Group(BossEventLoopGroup, WorkerEventLoopGroup)
        .ChildOption(ChannelOption.SoKeepalive, true)
        .Channel<TcpServerSocketChannel>().LocalAddress(_socketAddress)
        .ChildHandler(new Iso8583ChannelInitializer<ISocketChannel, ServerConfiguration>(
          Configuration, ConnectorConfigurator, WorkerEventLoopGroup,
          MessageFactory as IMessageFactory<IsoMessage>, MessageHandler
        ));
      ConfigureBootstrap(boostrap);
      boostrap.Validate();
      return boostrap;
    }

    /// <summary>
    ///   starts the iso 8583 server
    /// </summary>
    public async Task Start()
    {
      // initialize the client
      Init();

      // bind to socket and set the connection channel
      var channel = await GetBootstrap().BindAsync();
      SetChannel(channel);
    }

    /// <summary>
    ///   shutdowns the iso server gracefully
    /// </summary>
    public new async Task Shutdown()
    {
      await Stop();
      await base.Shutdown();
    }

    /// <summary>
    ///   stops the iso 8583 server
    /// </summary>
    private async Task Stop()
    {
      var channel = GetChannel();
      await channel.DeregisterAsync();
      await channel.CloseAsync();
    }
  }
}