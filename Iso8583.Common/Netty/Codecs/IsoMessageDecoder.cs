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

using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using Iso8583.Common.Metrics;
using NetCore8583;
using NetCore8583.Extensions;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   Decodes any Iso 8583 message coming from the wire
  /// </summary>
  public class IsoMessageDecoder : ByteToMessageDecoder
  {
    private readonly IMessageFactory<IsoMessage> _messageFactory;
    private readonly IIso8583Metrics _metrics;

    /// <summary>
    ///   creates a new instance of the decoder given the iso message factory
    /// </summary>
    /// <param name="messageFactory"></param>
    /// <param name="metrics">optional metrics provider</param>
    public IsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory, IIso8583Metrics metrics = null)
    {
      _messageFactory = messageFactory;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
    }

    protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
    {
      // return when the byte buffer cannot be read
      if (!input.IsReadable()) return;

      // create a byte array with the size of readable bytes in the byte buffer
      var bytea = new byte[input.ReadableBytes];

      // read the bytes from the byte buffer
      input.ReadBytes(bytea);

      // create a new iso message from the byte array
      var isoMessage = _messageFactory.ParseMessage(bytea, 0);
      if (isoMessage == null) throw new ParseException("Can't parse ISO8583 message");

      _metrics.MessageReceived(isoMessage.Type);
      output.Add(isoMessage);
    }
  }
}
