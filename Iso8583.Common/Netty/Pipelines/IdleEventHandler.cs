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
      if (evt is not IdleStateEvent { State: IdleState.ReaderIdle or IdleState.AllIdle }) return;
      var message = _messageFactory.NewMessage(MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
        MessageOrigin.ACQUIRER);

      // Propagate write failures to the pipeline instead of silently swallowing them
      context.WriteAndFlushAsync(message).ContinueWith(static (t, state) =>
      {
        var ctx = (IChannelHandlerContext)state!;
        if (t.Exception != null)
          ctx.FireExceptionCaught(t.Exception.InnerException ?? t.Exception);
      }, context, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
    }
  }
}
