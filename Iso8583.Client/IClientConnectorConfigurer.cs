using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using Iso8583.Common;

namespace Iso8583.Client
{
  
  /// <summary>
  ///  This interface helps configure the client bootstrap
  /// </summary>
  public interface IClientConnectorConfigurer<in T, in B> 
    where T : ConnectorConfiguration
    where B: Bootstrap
  {
    /// <summary>
    ///   Hook added before the completion of the bootstrap configuration
    /// </summary>
    /// <param name="bootstrap">the server bootstrap</param>
    /// <param name="configuration">the configuration</param>
    void ConfigureBootstrap(B bootstrap,
      T configuration);

    /// <summary>
    ///   Configures the pipeline.
    /// </summary>
    /// <param name="pipeline">the channel pipeline</param>
    /// <param name="configuration">the configuration</param>
    void ConfigurePipeline(IChannelPipeline pipeline,
      T configuration);
  }
}