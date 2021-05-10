using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;

namespace Iso8583.Client
{
  public class ClientConnectorConfigurator : IClientConnectorConfigurator<ClientConfiguration>
  {
    public void ConfigureBootstrap(Bootstrap bootstrap, ClientConfiguration configuration)
    {
      // this method was intentionally left blank
    }

    public void ConfigurePipeline(IChannelPipeline pipeline, ClientConfiguration configuration)
    {
      // this method was intentionally left blank
    }
  }
}