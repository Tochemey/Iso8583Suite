using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;

namespace Iso8583.Server
{
    /// <summary>
    ///   Server Connector Configuration
    /// </summary>
    public class ServerConnectorConfiguration : IServerConnectorConfigurer<ServerConfiguration, ServerBootstrap>
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