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
using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   A frame decoder for ISO8583 messages where the frame length header is encoded as an ASCII string.
  ///   For example, a message of 152 bytes would have a header of "0152" (4 ASCII characters).
  /// </summary>
  public class StringLengthFieldBasedFrameDecoder : ByteToMessageDecoder
  {
    private readonly int _maxFrameLength;
    private readonly int _lengthFieldOffset;
    private readonly int _lengthFieldLength;
    private readonly int _lengthAdjustment;
    private readonly int _initialBytesToStrip;

    /// <summary>
    ///   Creates a new instance of the string-based frame decoder.
    /// </summary>
    /// <param name="maxFrameLength">
    ///   The maximum length of the frame. If the length of the frame is greater than this value,
    ///   <see cref="TooLongFrameException" /> will be thrown.
    /// </param>
    /// <param name="lengthFieldOffset">The offset of the length field.</param>
    /// <param name="lengthFieldLength">The length of the length field (number of ASCII digits).</param>
    /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
    /// <param name="initialBytesToStrip">The number of first bytes to strip out from the decoded frame.</param>
    public StringLengthFieldBasedFrameDecoder(
      int maxFrameLength,
      int lengthFieldOffset,
      int lengthFieldLength,
      int lengthAdjustment,
      int initialBytesToStrip)
    {
      _maxFrameLength = maxFrameLength;
      _lengthFieldOffset = lengthFieldOffset;
      _lengthFieldLength = lengthFieldLength;
      _lengthAdjustment = lengthAdjustment;
      _initialBytesToStrip = initialBytesToStrip;
    }

    protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
    {
      var decoded = Decode(context, input);
      if (decoded != null)
      {
        output.Add(decoded);
      }
    }

    /// <summary>
    ///   Decodes a frame from the input buffer.
    /// </summary>
    private object Decode(IChannelHandlerContext context, IByteBuffer input)
    {
      // Check if we have enough bytes to read the length field
      if (input.ReadableBytes < _lengthFieldOffset + _lengthFieldLength)
      {
        return null;
      }

      // Read the length field as ASCII string
      var actualLengthFieldOffset = input.ReaderIndex + _lengthFieldOffset;
      var frameLength = GetFrameLength(input, actualLengthFieldOffset, _lengthFieldLength);

      // Apply length adjustment
      frameLength += _lengthAdjustment + _lengthFieldOffset + _lengthFieldLength;

      // Validate frame length
      if (frameLength < 0)
      {
        input.SkipBytes(_lengthFieldOffset + _lengthFieldLength);
        throw new CorruptedFrameException($"Negative frame length: {frameLength}");
      }

      if (frameLength > _maxFrameLength)
      {
        // Discard the bytes for this frame
        var bytesToDiscard = Math.Min(frameLength, input.ReadableBytes);
        input.SkipBytes((int)bytesToDiscard);
        throw new TooLongFrameException($"Frame length exceeds {_maxFrameLength}: {frameLength}");
      }

      // Check if we have enough bytes for the complete frame
      var frameLengthInt = (int)frameLength;
      if (input.ReadableBytes < frameLengthInt)
      {
        return null;
      }

      // Skip initial bytes if configured
      if (_initialBytesToStrip > frameLengthInt)
      {
        throw new CorruptedFrameException(
          $"Initial bytes to strip ({_initialBytesToStrip}) exceeds frame length ({frameLengthInt})");
      }

      input.SkipBytes(_initialBytesToStrip);

      // Extract the frame
      var readerIndex = input.ReaderIndex;
      var actualFrameLength = frameLengthInt - _initialBytesToStrip;
      var frame = input.RetainedSlice(readerIndex, actualFrameLength);
      input.SetReaderIndex(readerIndex + actualFrameLength);

      return frame;
    }

    /// <summary>
    ///   Parses the frame length from the ASCII-encoded length field.
    ///   Zero-allocation: reads individual bytes and computes the integer directly.
    /// </summary>
    private long GetFrameLength(IByteBuffer buffer, int offset, int length)
    {
      long frameLength = 0;
      for (var i = 0; i < length; i++)
      {
        var b = buffer.GetByte(offset + i);
        if (b < (byte)'0' || b > (byte)'9')
          throw new CorruptedFrameException(
            $"Invalid character in frame length field at position {i}: 0x{b:X2}");
        frameLength = frameLength * 10 + (b - '0');
      }

      return frameLength;
    }
  }
}
