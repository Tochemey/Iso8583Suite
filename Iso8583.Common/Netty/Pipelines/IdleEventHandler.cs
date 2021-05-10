using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   This handler helps listen to idle state in the network connectivity and send
  ///   an echo iso message as kind of health check
  /// </summary>
  public class IdleEventHandler : ChannelHandlerAdapter
  {
    private readonly IMessageFactory<IsoMessage> _messageFactory;

    /// <summary>
    ///   creates a new instance of <see cref="IdleEventHandler" />
    /// </summary>
    /// <param name="messageFactory"></param>
    public IdleEventHandler(IMessageFactory<IsoMessage> messageFactory) => _messageFactory = messageFactory;


    public override void UserEventTriggered(IChannelHandlerContext context, object evt)
    {
      if (evt is not IdleStateEvent {State: IdleState.ReaderIdle or IdleState.AllIdle}) return;
      var message = _messageFactory.NewMessage(MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
        MessageOrigin.ACQUIRER);
      context.WriteAndFlushAsync(message);
    }
  }
}