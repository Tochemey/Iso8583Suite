using System;
using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Util;

namespace Iso8583.Common.Netty.Pipelines
{
    /// <summary>
    ///   Iso Message parsing exception handler
    /// </summary>
    public class ParseExceptionHandler : ChannelHandlerAdapter
  {
    private readonly bool _includeErrorDetails;
    private readonly IMessageFactory<IsoMessage> _messageFactory;

    /// <summary>
    ///   creates a new instance of <see cref="ParseExceptionHandler" />
    /// </summary>
    /// <param name="messageFactory">the message factory instance</param>
    /// <param name="includeErrorDetails">state whether to include error details or not</param>
    public ParseExceptionHandler(IMessageFactory<IsoMessage> messageFactory, bool includeErrorDetails)
    {
      _messageFactory = messageFactory;
      _includeErrorDetails = includeErrorDetails;
    }

    /// <summary>
    ///   make the handler sharable
    /// </summary>
    public override bool IsSharable => true;

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
      if (exception is ParseException cause) context.WriteAndFlushAsync(CreateErrorResponseMessage(cause));

      context.FireExceptionCaught(exception);
    }

    /// <summary>
    ///   creates an iso message containing the parsing error response
    /// </summary>
    /// <param name="exception">the parsing error</param>
    /// <returns>an <see cref="IsoMessage" /> containing the error response</returns>
    private IsoMessage CreateErrorResponseMessage(ParseException exception)
    {
      // iso message that hold the error response
      var message = _messageFactory.NewMessage(MessageClass.ADMINISTRATIVE, MessageFunction.NOTIFICATION,
        MessageOrigin.OTHER);

      // 650 (Unable to parse message)
      message.SetValue(24, 650, IsoType.NUMERIC, 3);
      if (!_includeErrorDetails) return message;

      // construct the error details
      var details = exception.Message;
      if (details.Length > 25) details = $"{details[..22]}...";

      message.SetValue(44, details, IsoType.LLVAR, 25);

      return message;
    }
  }
}