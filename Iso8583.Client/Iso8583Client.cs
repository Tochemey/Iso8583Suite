using System;
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
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    /// <summary>
    ///   server address
    /// </summary>
    private string _host;

    /// <summary>
    ///   server port
    /// </summary>
    private int _port;

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
      if (channel is { IsWritable: true }) await channel.WriteAndFlushAsync(message);
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
      bootstrap.Group(WorkerEventLoopGroup);
      bootstrap.Channel<TcpSocketChannel>();
      bootstrap.Handler(new Iso8583ChannelInitializer<ISocketChannel, ClientConfiguration>(
        Configuration, ConnectorConfigurator, WorkerEventLoopGroup,
        MessageFactory as IMessageFactory<IsoMessage>, MessageHandler
      ));

      ConfigureBootstrap(bootstrap);
      bootstrap.Validate();
      return bootstrap;
    }


    /// <summary>
    ///   connects to the iso8583 server
    /// </summary>
    /// <param name="host">the iso server host</param>
    /// <param name="port">the iso server port</param>
    public async Task Connect(string host, int port)
    {
      // initialize the client
      Init();

      // bind to socket and set the connection channel
      var channel = await GetBootstrap().ConnectAsync(host, port);
      SetChannel(channel);
      _host = host;
      _port = port;
    }

    /// <summary>
    ///   disconnects from the iso8583 server
    /// </summary>
    public async Task Disconnect()
    {
      var channel = GetChannel();
      await channel.CloseAsync();
      await WorkerEventLoopGroup.ShutdownGracefullyAsync();
    }

    /// <summary>
    ///   checks whether the client is connected to the iso8583 server
    /// </summary>
    /// <returns></returns>
    public bool IsConnected()
    {
      var channel = GetChannel();
      return channel is { Active: true };
    }

    /// <summary>
    ///   try reconnect back when the connection is closed
    /// </summary>
    private async Task TryReconnect()
    {
      if (IsChannelInactive())
      {
        await _semaphoreSlim.WaitAsync();
        try
        {
          if (IsChannelInactive()) await Connect(_host, _port);
        }
        finally
        {
          _semaphoreSlim.Release();
        }
      }
    }
  }
}