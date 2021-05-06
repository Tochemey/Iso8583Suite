using System;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using NetCore8583;
using NetCore8583.Util;

namespace Iso8583.Common.Netty.Codecs
{
    /// <summary>
    ///     Encode iso message to sent over the wire
    /// </summary>
    public class IsoMessageEncoder : MessageToByteEncoder<IsoMessage>
    {
        private readonly bool _encodeLengthHeaderAsString;
        private readonly int _lengthHeaderLength;

        /// <summary>
        ///     creates a new instance of the encoder
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
            switch (_lengthHeaderLength)
            {
                case 0:
                    output.WriteBytes(message.WriteData().ToUnsignedBytes());
                    break;
                default:
                {
                    if (_encodeLengthHeaderAsString)
                    {
                        var bytea = message.WriteData().ToUnsignedBytes();
                        var lengthHeader = Convert.ToString(bytea.Length).PadLeft(_lengthHeaderLength, '0');
                        output.WriteBytes(lengthHeader.GetBytes());
                        output.WriteBytes(bytea);
                    }
                    else
                    {
                        output.WriteBytes(message.WriteToBuffer(_lengthHeaderLength).ToUnsignedBytes());
                    }

                    break;
                }
            }
        }
    }
}