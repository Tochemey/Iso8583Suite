using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;

namespace Iso8583.Server
{
  /// <summary>
  ///   Server Connector Configurator
  /// </summary>
  public class ServerConnectorConfigurator : IServerConnectorConfigurator<ServerConfiguration>
  {
    public void ConfigureBootstrap(ServerBootstrap bootstrap, ServerConfiguration configuration)
    {
      // this method was intentionally left blank
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ServerConfiguration configuration)
    {
      // this method was intentionally left blank
    }
  }
}