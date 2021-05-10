using DotNetty.Transport.Channels;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   listens to a closed client connection and attempt to reconnect
  /// </summary>
  public class ReconnectOnCloseListener : ChannelHandlerAdapter
  {
  }
}