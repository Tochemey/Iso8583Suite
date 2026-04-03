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
using System.Runtime.CompilerServices;
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Iso8583.Common.Metrics;
using NetCore8583;

namespace Iso8583.Common.Netty.Codecs
{
  /// <summary>
  ///   Encode iso message to sent over the wire
  /// </summary>
  public class IsoMessageEncoder : MessageToByteEncoder<IsoMessage>
  {
    private readonly bool _encodeLengthHeaderAsString;
    private readonly int _lengthHeaderLength;
    private readonly IIso8583Metrics _metrics;

    /// <summary>
    ///   creates a new instance of the encoder
    /// </summary>
    /// <param name="lengthHeaderLength">Number of bytes used for the length header (0 to omit the header).</param>
    /// <param name="encodeLengthHeaderAsString">
    ///   When <c>true</c>, the length header is written as ASCII digits (e.g. "0152").
    ///   When <c>false</c>, the length is written as a binary integer by the underlying NetCore8583 writer.
    /// </param>
    /// <param name="metrics">optional metrics provider</param>
    public IsoMessageEncoder(int lengthHeaderLength, bool encodeLengthHeaderAsString,
      IIso8583Metrics metrics = null)
    {
      _lengthHeaderLength = lengthHeaderLength;
      _encodeLengthHeaderAsString = encodeLengthHeaderAsString;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
    }

    /// <summary>
    ///   Serializes an <see cref="IsoMessage"/> into the outbound <paramref name="output"/> buffer,
    ///   prepending a length header when configured, and records a <see cref="IIso8583Metrics.MessageSent"/> metric.
    /// </summary>
    protected override void Encode(IChannelHandlerContext context, IsoMessage message, IByteBuffer output)
    {
      switch (_lengthHeaderLength)
      {
        case 0:
        {
          var data = SBytesToBytes(message.WriteData());
          output.WriteBytes(data);
          break;
        }
        default:
        {
          if (_encodeLengthHeaderAsString)
          {
            var data = SBytesToBytes(message.WriteData());
            WriteLengthHeaderAscii(output, data.Length, _lengthHeaderLength);
            output.WriteBytes(data);
          }
          else
          {
            var data = SBytesToBytes(message.WriteToBuffer(_lengthHeaderLength));
            output.WriteBytes(data);
          }

          break;
        }
      }

      _metrics.MessageSent(message.Type);
    }

    /// <summary>
    ///   Converts sbyte[] to byte[] using block copy (same memory layout, single allocation).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] SBytesToBytes(sbyte[] source)
    {
      var dest = new byte[source.Length];
      Buffer.BlockCopy(source, 0, dest, 0, source.Length);
      return dest;
    }

    /// <summary>
    ///   Writes the length header as ASCII digits directly into the buffer.
    ///   Avoids string allocation from Convert.ToString + PadLeft + GetBytes.
    /// </summary>
    private static void WriteLengthHeaderAscii(IByteBuffer buffer, int length, int headerLength)
    {
      // Write ASCII digits right-to-left, then pad with '0'
      Span<byte> digits = stackalloc byte[headerLength];
      digits.Fill((byte)'0');

      var value = length;
      for (var i = headerLength - 1; i >= 0 && value > 0; i--)
      {
        digits[i] = (byte)('0' + value % 10);
        value /= 10;
      }

      for (var i = 0; i < headerLength; i++)
        buffer.WriteByte(digits[i]);
    }
  }
}
