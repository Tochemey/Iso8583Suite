using System.Collections.Generic;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Util;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   Decodes any Iso 8583 message coming from the wire
  /// </summary>
  public class IsoMessageDecoder : ByteToMessageDecoder
  {
    private readonly IMessageFactory<IsoMessage> _messageFactory;

    /// <summary>
    ///   creates a new instance of the decoder given the iso message factory
    /// </summary>
    /// <param name="messageFactory"></param>
    public IsoMessageDecoder(IMessageFactory<IsoMessage> messageFactory) => _messageFactory = messageFactory;


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

      output.Add(isoMessage);
    }
  }
}