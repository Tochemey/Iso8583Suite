using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Server
{
  public abstract class ServerConnector<T, TC> : Iso8583Connector<T, TC>
    where T : IsoMessage
    where TC : ConnectorConfiguration
  {
    /// <summary>
    ///   the server bootstrap. <see cref="ServerBootstrap" />
    /// </summary>
    private ServerBootstrap _bootstrap;

    /// <summary>
    ///   creates a new instance of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageHandler">the message handler</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ServerConnector(CompositeIsoMessageHandler<T> messageHandler,
      IMessageFactory<T> messageFactory,
      TC configuration) : base(messageHandler, messageFactory, configuration)
    {
    }

    /// <summary>
    ///   auxiliary constructor to create a new of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ServerConnector(IMessageFactory<T> messageFactory,
      TC configuration) : base(messageFactory, configuration)
    {
    }


    /// <summary>
    ///   the connector configurator
    /// </summary>
    protected IServerConnectorConfigurator<TC> ConnectorConfigurator { get; set; }

    protected abstract ServerBootstrap CreateBootstrap();

    protected ServerBootstrap GetBootstrap() => _bootstrap;

    /// <summary>
    ///   initialize the server
    /// </summary>
    protected override void Init()
    {
      BossEventLoopGroup = CreateBossEventLoopGroup();
      WorkerEventLoopGroup = CreateWorkerEventLoopGroup();
      _bootstrap = CreateBootstrap();
    }


    /// <summary>
    ///   configures the server bootstrap
    /// </summary>
    /// <param name="bootstrap">the server bootstrap</param>
    protected void ConfigureBootstrap(ServerBootstrap bootstrap)
    {
      bootstrap
        .Option(ChannelOption.TcpNodelay, true)
        .Option(ChannelOption.AutoRead, true);

      ConnectorConfigurator?.ConfigureBootstrap(bootstrap,
        Configuration);
    }
  }
}