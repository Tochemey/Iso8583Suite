using System;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   used by the client when there is a disconnection
  /// </summary>
  public class ReconnectOnCloseHandler : ChannelHandlerAdapter
  {
    private readonly Func<EndPoint, Task> _connectFunc;
    private readonly int _retryAfter;

    public ReconnectOnCloseHandler(Func<EndPoint, Task> connectFunc, int retryAfter)
    {
      _connectFunc = connectFunc;
      _retryAfter = retryAfter;
    }

    public override void ChannelInactive(IChannelHandlerContext context)
    {
      base.ChannelInactive(context);
      // TODO better logging
      Console.WriteLine("ChannelInactive connected to {0}", context.Channel.RemoteAddress);

      context.Channel.EventLoop.Schedule(_ => _connectFunc((EndPoint)_), context.Channel.RemoteAddress,
        TimeSpan.FromMilliseconds(_retryAfter));
    }
  }
}