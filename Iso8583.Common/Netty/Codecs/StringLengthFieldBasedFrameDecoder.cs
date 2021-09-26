using System;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   DotNetty's [LengthFieldBasedFrameDecoder] assumes the frame length header is a binary encoded integer.
  ///   This overrides it's frame length decoding to implement the case when the frame length header is String encoded.
  /// </summary>
  public class StringLengthFieldBasedFrameDecoder : LengthFieldBasedFrameDecoder
  {
    /// <summary>
    ///   <see cref="LengthFieldBasedFrameDecoder" />
    /// </summary>
    /// <param name="maxFrameLength">
    ///   the maximum length of the frame.  If the length of the frame is  greater than this value
    ///   <see cref="TooLongFrameException" /> will be thrown
    /// </param>
    /// <param name="lengthFieldOffset">the offset of the length field</param>
    /// <param name="lengthFieldLength">the length of the length field</param>
    /// <param name="lengthAdjustment">the compensation value to add to the value of the length field</param>
    /// <param name="initialBytesToStrip">the number of first bytes to strip out from the decoded frame</param>
    public StringLengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength,
      int lengthAdjustment, int initialBytesToStrip) : base(maxFrameLength, lengthFieldOffset, lengthFieldLength,
      lengthAdjustment, initialBytesToStrip)
    {
    }


    /// <summary>
    ///   Decodes the specified region of the buffer into an unadjusted frame length
    /// </summary>
    /// <param name="buffer">the buffer we'll be extracting the frame length from</param>
    /// <param name="offset">the offset from the absolute <see cref="P:DotNetty.Buffers.IByteBuffer.ReaderIndex" /></param>
    /// <param name="length">the length of the frame length field</param>
    /// <param name="order">the preferred <see cref="T:DotNetty.Buffers.ByteOrder" /> of buffer.</param>
    /// <returns>a long integer that represents the unadjusted length of the next frame</returns>
    protected new long GetUnadjustedFrameLength(
      IByteBuffer buffer,
      int offset,
      int length,
      ByteOrder order)
    {
      var bytea = new byte[length];
      buffer.GetBytes(offset, bytea);
      if (order == ByteOrder.LittleEndian) Array.Reverse(bytea);

      return Encoding.Unicode.GetString(bytea).Length;
    }
  }
}