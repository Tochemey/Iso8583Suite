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

using DotNetty.Common.Concurrency;
using DotNetty.Transport.Channels;
using Iso8583.Common.Validation;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Channel handler that applies a <see cref="MessageValidator"/> to every inbound and
  ///   outbound <see cref="IsoMessage"/>. The handler is always installed in the pipeline so
  ///   that a validator can be attached or swapped at runtime via configuration; when the
  ///   configured validator is <c>null</c>, the handler is a transparent pass-through.
  ///
  ///   <para>
  ///   On outbound writes, a failing validation completes the write promise with a
  ///   <see cref="MessageValidationException"/> so that the caller sees the failure
  ///   synchronously and the invalid bytes never reach the wire.
  ///   </para>
  ///   <para>
  ///   On inbound reads, a failing validation fires an exception on the pipeline via
  ///   <see cref="IChannelHandlerContext.FireExceptionCaught"/>. Existing error handlers
  ///   (for example <see cref="ParseExceptionHandler"/> on servers configured with
  ///   <c>ReplyOnError</c>) can react. The invalid message is not forwarded upstream.
  ///   </para>
  /// </summary>
  public sealed class MessageValidationHandler : ChannelHandlerAdapter
  {
    private readonly MessageValidator _validator;

    /// <summary>
    ///   Create a new handler. Passing <c>null</c> makes the handler a pass-through.
    /// </summary>
    public MessageValidationHandler(MessageValidator validator)
    {
      _validator = validator;
    }

    /// <summary>
    ///   The handler is stateless and may be shared across multiple channels (used by the
    ///   pooled client and the server bootstrap).
    /// </summary>
    public override bool IsSharable => true;

    /// <inheritdoc />
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
      if (_validator == null || message is not IsoMessage isoMessage)
      {
        context.FireChannelRead(message);
        return;
      }

      var report = _validator.Validate(isoMessage);
      if (report.IsValid)
      {
        context.FireChannelRead(message);
        return;
      }

      // Invalid inbound message: surface as a pipeline exception and drop the message.
      context.FireExceptionCaught(new MessageValidationException(report));
    }

    /// <inheritdoc />
    public override void Write(IChannelHandlerContext context, object message, IPromise promise)
    {
      if (_validator == null || message is not IsoMessage isoMessage)
      {
        context.WriteAsync(message, promise);
        return;
      }

      var report = _validator.Validate(isoMessage);
      if (report.IsValid)
      {
        context.WriteAsync(message, promise);
        return;
      }

      promise.TrySetException(new MessageValidationException(report));
    }
  }
}
