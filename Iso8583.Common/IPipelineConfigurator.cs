using DotNetty.Transport.Channels;

namespace Iso8583.Common
{
  public interface IPipelineConfigurator<in T> where T : ConnectorConfiguration
  {
    /// <summary>
    ///   Configures the pipeline.
    /// </summary>
    /// <param name="pipeline">the channel pipeline</param>
    /// <param name="configuration">the configuration</param>
    void ConfigurePipeline(IChannelPipeline pipeline,
      T configuration);
  }
}