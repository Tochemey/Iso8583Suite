using DotNetty.Transport.Bootstrapping;
using Iso8583.Common;

namespace Iso8583.Server
{
  /// <summary>
  ///   This interface helps configure the server bootstrap
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface IServerConnectorConfigurator<in T> : IPipelineConfigurator<T>
    where T : ConnectorConfiguration
  {
    /// <summary>
    ///   Hook added before the completion of the bootstrap configuration
    /// </summary>
    /// <param name="bootstrap">the server bootstrap</param>
    /// <param name="configuration">the configuration</param>
    void ConfigureBootstrap(ServerBootstrap bootstrap,
      T configuration);
  }
}