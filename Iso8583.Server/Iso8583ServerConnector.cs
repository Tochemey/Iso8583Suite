using System.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Server
{
  public abstract class Iso8583ServerConnector<T, B, C>
    where T : IsoMessage
    where C : ConnectorConfiguration
    where B : ServerBootstrap
  {
    private readonly AtomicReference<IChannel> _channelRef;

    /// <summary>
    ///   the server bootstrap. <see cref="ServerBootstrap" />
    /// </summary>
    private B _bootstrap;

    /// <summary>
    ///   creates a new instance of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageHandler">the message handler</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected Iso8583ServerConnector(CompositeIsoMessageHandler<T> messageHandler,
      IMessageFactory<T> messageFactory,
      C configuration)
    {
      MessageHandler = messageHandler;
      MessageFactory = messageFactory;
      Configuration = configuration;
      _channelRef = new AtomicReference<IChannel>();
      if (configuration.AddEchoMessageListener)
        MessageHandler.AddListener(new EchoMessageListener<T>(messageFactory));
    }

    /// <summary>
    ///   auxiliary constructor to create a new of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected Iso8583ServerConnector(IMessageFactory<T> messageFactory,
      C configuration)
    {
      MessageHandler = new CompositeIsoMessageHandler<T>();
      MessageFactory = messageFactory;
      Configuration = configuration;
      _channelRef = new AtomicReference<IChannel>();
      if (configuration.AddEchoMessageListener)
        MessageHandler.AddListener(new EchoMessageListener<T>(messageFactory));
    }

    /// <summary>
    ///   the message handler
    /// </summary>
    protected CompositeIsoMessageHandler<T> MessageHandler { get; }

    /// <summary>
    ///   the message factory
    /// </summary>
    protected IMessageFactory<T> MessageFactory { get; }

    /// <summary>
    ///   the server configuration
    /// </summary>
    protected C Configuration { get; }

    /// <summary>
    ///   the connector configurer
    /// </summary>
    protected IServerConnectorConfigurer<C, B> ConnectorConfigurer { get; set; }

    /// <summary>
    ///   the boss event loop group. <see cref="MultithreadEventLoopGroup" />
    /// </summary>
    protected MultithreadEventLoopGroup BossEventLoopGroup { get; private set; }

    /// <summary>
    ///   the worker thread event loop group. <see cref="MultithreadEventLoopGroup" />
    /// </summary>
    protected MultithreadEventLoopGroup WorkerEventLoopGroup { get; private set; }

    protected abstract B CreateBootstrap();

    protected B GetBootstrap()
    {
      return _bootstrap;
    }

    /// <summary>
    ///   initialize the server
    /// </summary>
    public void Init()
    {
      BossEventLoopGroup = CreateBossEventLoopGroup();
      WorkerEventLoopGroup = CreateWorkerEventLoopGroup();
      _bootstrap = CreateBootstrap();
    }


    /// <summary>
    ///   creates the boss worker thread group
    /// </summary>
    /// <returns></returns>
    protected MultithreadEventLoopGroup CreateBossEventLoopGroup()
    {
      return new(1);
    }

    /// <summary>
    ///   creates the worker threads group
    /// </summary>
    protected MultithreadEventLoopGroup CreateWorkerEventLoopGroup()
    {
      var group = new MultithreadEventLoopGroup(Configuration.WorkerThreadCount);
      return group;
    }


    /// <summary>
    ///   shutdown the system
    /// </summary>
    protected async Task Shutdown()
    {
      if (WorkerEventLoopGroup != null)
      {
        await WorkerEventLoopGroup.ShutdownGracefullyAsync();
        WorkerEventLoopGroup = null;
      }

      if (BossEventLoopGroup != null)
      {
        await BossEventLoopGroup.ShutdownGracefullyAsync();
        BossEventLoopGroup = null;
      }
    }


    /// <summary>
    ///   configures the server bootstrap
    /// </summary>
    /// <param name="bootstrap">the server bootstrap</param>
    protected void ConfigureBootstrap(B bootstrap)
    {
      bootstrap
        .ChildOption(ChannelOption.TcpNodelay, true)
        .ChildOption(ChannelOption.AutoRead, true);

      ConnectorConfigurer?.ConfigureBootstrap(bootstrap,
        Configuration);
    }


    /// <summary>
    ///   adds a iso message handler
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void AddMessageListener(IIsoMessageListener<T> handler)
    {
      MessageHandler.AddListener(handler);
    }

    /// <summary>
    ///   removes an iso message handler
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void RemoveMessageListener(IIsoMessageListener<T> handler)
    {
      MessageHandler.RemoveListener(handler);
    }


    /// <summary>
    ///   sets the network channel
    /// </summary>
    /// <param name="channel">the channel</param>
    protected void SetChannel(IChannel channel)
    {
      _channelRef.GetAndSet(channel);
    }

    /// <summary>
    ///   gets the network channel
    /// </summary>
    /// <returns></returns>
    protected IChannel GetChannel()
    {
      return _channelRef.Value;
    }
  }
}