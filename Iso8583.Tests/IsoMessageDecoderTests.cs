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
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Codecs;
using NetCore8583;
using NetCore8583.Extensions;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class IsoMessageDecoderTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public IsoMessageDecoderTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    [Fact]
    public void Decode_ValidMessage_ProducesIsoMessage()
    {
        var original = _factory.NewMessage(0x0200);
        original.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        original.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));

        var sbytes = original.WriteData();
        var bytes = new byte[sbytes.Length];
        Buffer.BlockCopy(sbytes, 0, bytes, 0, sbytes.Length);

        var decoder = new IsoMessageDecoder(_factory);
        var buffer = Unpooled.WrappedBuffer(bytes);
        var output = new List<object>();

        try
        {
            // Use reflection to call protected Decode
            var method = typeof(IsoMessageDecoder).GetMethod("Decode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method!.Invoke(decoder, [null, buffer, output]);

            Assert.Single(output);
            var decoded = output[0] as IsoMessage;
            Assert.NotNull(decoded);
            Assert.Equal(0x0200, decoded.Type);
        }
        finally
        {
            buffer.Release();
        }
    }

    [Fact]
    public void Decode_EmptyBuffer_ProducesNothing()
    {
        var decoder = new IsoMessageDecoder(_factory);
        var buffer = Unpooled.Buffer(0);
        var output = new List<object>();

        var method = typeof(IsoMessageDecoder).GetMethod("Decode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(decoder, [null, buffer, output]);

        Assert.Empty(output);
        buffer.Release();
    }

    [Fact]
    public void Decode_InvalidData_ThrowsParseException()
    {
        var decoder = new IsoMessageDecoder(_factory);
        var buffer = Unpooled.WrappedBuffer(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
        var output = new List<object>();

        var method = typeof(IsoMessageDecoder).GetMethod("Decode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        try
        {
            var ex = Assert.ThrowsAny<Exception>(() => method!.Invoke(decoder, [null, buffer, output]));
            // TargetInvocationException wraps the ParseException
            Assert.NotNull(ex);
        }
        finally
        {
            buffer.Release();
        }
    }
}
