using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
  public class Iso8583Client<T> : ClientConnector<T, ClientConfiguration>, ISend where T : IsoMessage
  {
    /// <summary>
    ///   server port
    /// </summary>
    private IPEndPoint _endPoint;

    /// <summary>
    ///   creates a new instance of <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="messageFactory"></param>
    public Iso8583Client(ClientConfiguration configuration,
      IMessageFactory<T> messageFactory) : base(
      messageFactory, configuration)
    {
    }

    /// <summary>
    ///   create a new instance of  <see cref="Iso8583Client{T}" />
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    public Iso8583Client(IMessageFactory<T> messageFactory) : base(messageFactory, new ClientConfiguration())
    {
    }

    /// <inheritdoc />
    public async Task Send(IsoMessage message)
    {
      // get the connection channel
      var channel = GetChannel();

      // send the message when the channel is writable
      if (channel is {IsWritable: true}) await channel.WriteAndFlushAsync(message);
    }

    /// <inheritdoc />
    public async Task Send(IsoMessage message, int timeout)
    {
      var cts = new CancellationTokenSource();
      using (cts)
      {
        try
        {
          cts.CancelAfter(TimeSpan.FromMilliseconds(timeout));
          await Send(message);
        }
        catch (Exception e)
        {
          // TODO better logging
          Console.WriteLine(e);
        }
      }
    }

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

      // TODO handle reconnection
      return bootstrap;
    }

    /// <summary>
    ///   connects to the iso 8583 server
    /// </summary>
    public async Task Connect(IPEndPoint address)
    {
      _endPoint = address;
      // TODO handle reconnection
      var bootstrap = GetBootstrap();
      var channel = await bootstrap.ConnectAsync(_endPoint);
      SetChannel(channel);
    }

    /// <summary>
    ///   connects to the iso8583 server
    /// </summary>
    /// <param name="host">the iso server host</param>
    /// <param name="port">the iso server port</param>
    public async Task Connect(string host, int port)
    {
      var endpoint = new IPEndPoint(IPAddress.Parse(host), port);
      // set the endpoint channel
      _endPoint = endpoint;
      await Connect(_endPoint);
    }

    /// <summary>
    ///   disconnects from the iso8583 server
    /// </summary>
    public async Task Disconnect()
    {
      var channel = GetChannel();
      await channel.CloseAsync();
      await Shutdown();
    }
  }
}