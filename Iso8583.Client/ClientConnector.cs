using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Client
{
  public abstract class ClientConnector<T, TC> : Iso8583Connector<T, TC>
    where T : IsoMessage
    where TC : ConnectorConfiguration
  {
    /// <summary>
    ///   the client bootstrap. <see cref="Bootstrap" />
    /// </summary>
    private Bootstrap _bootstrap;

    /// <summary>
    ///   creates a new instance of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageHandler">the message handler</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ClientConnector(CompositeIsoMessageHandler<T> messageHandler,
      IMessageFactory<T> messageFactory,
      TC configuration) : base(messageHandler, messageFactory, configuration)
    {
    }

    /// <summary>
    ///   auxiliary constructor to create a new of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ClientConnector(IMessageFactory<T> messageFactory,
      TC configuration) : base(messageFactory, configuration)
    {
    }

    /// <summary>
    ///   the connector configurator
    /// </summary>
    protected IClientConnectorConfigurator<TC> ConnectorConfigurator { get; set; }

    protected abstract Bootstrap CreateBootstrap();

    protected Bootstrap GetBootstrap() => _bootstrap;

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
    ///   configures the client bootstrap
    /// </summary>
    /// <param name="bootstrap">the client bootstrap</param>
    protected void ConfigureBootstrap(Bootstrap bootstrap)
    {
      bootstrap
        .Option(ChannelOption.TcpNodelay, true)
        .Option(ChannelOption.AutoRead, true);

      ConnectorConfigurator?.ConfigureBootstrap(bootstrap,
        Configuration);
    }
  }
}