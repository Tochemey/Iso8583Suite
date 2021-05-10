using Iso8583.Common;

namespace Iso8583.Client
{
  /// <summary>
  ///   Client configuration
  /// </summary>
  public class ClientConfiguration : ConnectorConfiguration
  {
    /// <summary>
    ///   Default client reconnect interval in milliseconds.
    /// </summary>
    private readonly int _reconnectInterval;

    /// <summary>
    ///   create a new instance of ClientConfiguration
    /// </summary>
    /// <param name="reconnectInterval"></param>
    public ClientConfiguration(int reconnectInterval) => _reconnectInterval = reconnectInterval;

    /// <summary>
    ///   default constructor
    /// </summary>
    public ClientConfiguration() => _reconnectInterval = 100;
  }
}