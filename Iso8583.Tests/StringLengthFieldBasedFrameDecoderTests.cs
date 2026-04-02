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
using System.Text;
using DotNetty.Buffers;
using DotNetty.Codecs;
using Iso8583.Common.Netty.Codecs;
using Xunit;

namespace Iso8583.Tests;

public class StringLengthFieldBasedFrameDecoderTests
{
    [Fact]
    public void Decode_ValidFrame_ExtractsPayload()
    {
        // 4-byte ASCII length header "0005" + 5 bytes of payload
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 4, 0, 4);
        var data = Encoding.ASCII.GetBytes("0005HELLO");
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        InvokeDecode(decoder, buffer, output);

        Assert.Single(output);
        var frame = output[0] as IByteBuffer;
        Assert.NotNull(frame);
        Assert.Equal(5, frame.ReadableBytes);
        var payload = new byte[5];
        frame.ReadBytes(payload);
        Assert.Equal("HELLO", Encoding.ASCII.GetString(payload));

        frame.Release();
        buffer.Release();
    }

    [Fact]
    public void Decode_2ByteLengthField_Works()
    {
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 2, 0, 2);
        var data = Encoding.ASCII.GetBytes("03ABC");
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        InvokeDecode(decoder, buffer, output);

        Assert.Single(output);
        var frame = output[0] as IByteBuffer;
        Assert.NotNull(frame);
        Assert.Equal(3, frame.ReadableBytes);
        frame.Release();
        buffer.Release();
    }

    [Fact]
    public void Decode_InsufficientBytes_ReturnsNothing()
    {
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 4, 0, 4);
        // Only 2 bytes, need at least 4 for the length header
        var buffer = Unpooled.WrappedBuffer(new byte[] { (byte)'0', (byte)'0' });
        var output = new List<object>();

        InvokeDecode(decoder, buffer, output);

        Assert.Empty(output);
        buffer.Release();
    }

    [Fact]
    public void Decode_IncompleteFrame_WaitsForMoreData()
    {
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 4, 0, 4);
        // Header says 10 bytes but only 3 bytes of payload present
        var data = Encoding.ASCII.GetBytes("0010ABC");
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        InvokeDecode(decoder, buffer, output);

        Assert.Empty(output); // waiting for more data
        buffer.Release();
    }

    [Fact]
    public void Decode_FrameTooLong_ThrowsTooLongFrameException()
    {
        var decoder = new StringLengthFieldBasedFrameDecoder(10, 0, 4, 0, 4);
        // Length says 9999 which exceeds max of 10
        var data = Encoding.ASCII.GetBytes("9999X");
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        Assert.ThrowsAny<Exception>(() => InvokeDecode(decoder, buffer, output));
        buffer.Release();
    }

    [Fact]
    public void Decode_InvalidAsciiInLength_ThrowsCorruptedFrameException()
    {
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 4, 0, 4);
        // 'AB12' is not valid - first two chars are non-digit
        var data = new byte[] { (byte)'A', (byte)'B', (byte)'1', (byte)'2', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        Assert.ThrowsAny<Exception>(() => InvokeDecode(decoder, buffer, output));
        buffer.Release();
    }

    [Fact]
    public void Decode_WithLengthAdjustment_AdjustsCorrectly()
    {
        // Length field says 9 (5 payload + 4 length itself), adjustment = -4
        var decoder = new StringLengthFieldBasedFrameDecoder(8192, 0, 4, -4, 4);
        var data = Encoding.ASCII.GetBytes("0009HELLO");
        var buffer = Unpooled.WrappedBuffer(data);
        var output = new List<object>();

        InvokeDecode(decoder, buffer, output);

        Assert.Single(output);
        var frame = output[0] as IByteBuffer;
        Assert.NotNull(frame);
        Assert.Equal(5, frame.ReadableBytes);
        frame.Release();
        buffer.Release();
    }

    private static void InvokeDecode(StringLengthFieldBasedFrameDecoder decoder,
        IByteBuffer buffer, List<object> output)
    {
        var method = typeof(StringLengthFieldBasedFrameDecoder).GetMethod("Decode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, [typeof(DotNetty.Transport.Channels.IChannelHandlerContext), typeof(IByteBuffer), typeof(List<object>)], null);
        method!.Invoke(decoder, [null, buffer, output]);
    }
}
