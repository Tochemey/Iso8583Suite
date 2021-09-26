using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using NetCore8583;
using NetCore8583.Util;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   Encode iso message to sent over the wire
  /// </summary>
  public class IsoMessageEncoder : MessageToByteEncoder<IsoMessage>
  {
    private readonly bool _encodeLengthHeaderAsString;
    private readonly int _lengthHeaderLength;

    /// <summary>
    ///   creates a new instance of the encoder
    /// </summary>
    /// <param name="lengthHeaderLength"></param>
    /// <param name="encodeLengthHeaderAsString"></param>
    public IsoMessageEncoder(int lengthHeaderLength, bool encodeLengthHeaderAsString)
    {
      _lengthHeaderLength = lengthHeaderLength;
      _encodeLengthHeaderAsString = encodeLengthHeaderAsString;
    }

    protected override void Encode(IChannelHandlerContext context, IsoMessage message, IByteBuffer output)
    {
      sbyte[] bytea;
      byte[] streamToSend;
      switch (_lengthHeaderLength)
      {
        case 0:
          bytea = message.WriteData();
          streamToSend = Encoding.ASCII.GetBytes(bytea.ToString(Encoding.ASCII));
          output.WriteBytes(streamToSend);
          break;
        default:
        {
          if (_encodeLengthHeaderAsString)
          {
            bytea = message.WriteData();
            streamToSend = Encoding.ASCII.GetBytes(bytea.ToString(Encoding.ASCII));
            var lengthHeader = Convert.ToString(streamToSend.Length).PadLeft(_lengthHeaderLength, '0');
            output.WriteBytes(lengthHeader.GetBytes());
            output.WriteBytes(streamToSend);
          }
          else
          {
            bytea = message.WriteToBuffer(_lengthHeaderLength);
            streamToSend = Encoding.ASCII.GetBytes(bytea.ToString(Encoding.ASCII));
            output.WriteBytes(streamToSend);
          }

          break;
        }
      }
    }
  }
}