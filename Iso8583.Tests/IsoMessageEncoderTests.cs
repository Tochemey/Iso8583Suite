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
using System.Text;
using DotNetty.Buffers;
using Iso8583.Common.Netty.Codecs;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class IsoMessageEncoderTests : IDisposable
{
    private readonly IsoMessage _message;
    private readonly IByteBuffer _buffer;

    public IsoMessageEncoderTests()
    {
        var mfact = ConfigParser.CreateDefault();
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        _message = mfact.NewMessage(0x0200);
        _message.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        _message.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        _message.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        _buffer = Unpooled.Buffer(1024);
    }

    public void Dispose() => _buffer.Release();

    [Fact]
    public void Encode_NoLengthHeader_WritesRawMessage()
    {
        var encoder = new IsoMessageEncoder(0, false);
        encoder.DoEncode(null!, _message, _buffer);

        Assert.True(_buffer.ReadableBytes > 0);
        // Should start with MTI "0200"
        var bytes = new byte[4];
        _buffer.GetBytes(0, bytes);
        Assert.Equal("0200", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Encode_StringLengthHeader_PrependsPaddedLength()
    {
        var encoder = new IsoMessageEncoder(4, true);
        encoder.DoEncode(null!, _message, _buffer);

        // First 4 bytes should be ASCII length
        var headerBytes = new byte[4];
        _buffer.GetBytes(0, headerBytes);
        var lengthStr = Encoding.ASCII.GetString(headerBytes);
        Assert.True(int.TryParse(lengthStr, out var length));
        Assert.True(length > 0);

        // Total buffer should be header + message
        Assert.Equal(4 + length, _buffer.ReadableBytes);
    }

    [Fact]
    public void Encode_BinaryLengthHeader_WritesWithHeader()
    {
        var encoder = new IsoMessageEncoder(2, false);
        encoder.DoEncode(null!, _message, _buffer);
        Assert.True(_buffer.ReadableBytes > 2);
    }

    [Fact]
    public void Encode_StringLengthHeader_2Bytes_OverflowThrows()
    {
        var encoder = new IsoMessageEncoder(2, true);
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            encoder.DoEncode(null!, _message, _buffer));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }
}

// Extension to expose the protected Encode method for testing
public static class EncoderTestExtensions
{
    public static void DoEncode(this IsoMessageEncoder encoder,
        DotNetty.Transport.Channels.IChannelHandlerContext ctx, IsoMessage message, IByteBuffer output)
    {
        // Use reflection to call the protected Encode method
        var method = typeof(IsoMessageEncoder).GetMethod("Encode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(encoder, [ctx, message, output]);
    }
}
