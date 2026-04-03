// Copyright 2021-2026 Arsene Tochemey Gandote
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Extensions;

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

    /// <summary>
    ///   If the exception is a <see cref="NetCore8583.Extensions.ParseException"/>, sends an administrative
    ///   error response (function code 650) to the remote peer before propagating the exception.
    /// </summary>
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