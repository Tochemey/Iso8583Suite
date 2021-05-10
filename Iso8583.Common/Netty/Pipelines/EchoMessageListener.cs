using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   listens to the iso Echo message and handle it
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class EchoMessageListener<T> : IIsoMessageListener<T> where T : IsoMessage
  {
    private readonly IMessageFactory<T> _messageFactory;

    /// <summary>
    ///   creates a new instance of the EchoMessageListener
    /// </summary>
    /// <param name="messageFactory"></param>
    public EchoMessageListener(IMessageFactory<T> messageFactory) => _messageFactory = messageFactory;

    /// <inheritdoc />
    public bool CanHandleMessage(T isoMessage) =>
      isoMessage != null && (isoMessage.Type & (int) MessageClass.NETWORK_MANAGEMENT) != 0;

    /// <summary>
    ///   sends EchoResponse message. Always returns <code>false</code>.
    /// </summary>
    /// <param name="context">the channel handler context</param>
    /// <param name="isoMessage">the message to handle</param>
    /// <returns><code>false</code> - message should not be handled by any other handler</returns>
    public bool HandleMessage(IChannelHandlerContext context, T isoMessage)
    {
      var response = _messageFactory.CreateResponse(isoMessage);
      context.WriteAndFlushAsync(response);
      return false;
    }
  }
}