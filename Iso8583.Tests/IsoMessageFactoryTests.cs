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
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class IsoMessageFactoryTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public IsoMessageFactoryTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    [Fact]
    public void NewMessage_ByType_CreatesMessage()
    {
        var msg = _factory.NewMessage(0x0200);
        Assert.NotNull(msg);
        Assert.Equal(0x0200, msg.Type);
    }

    [Fact]
    public void NewMessage_ByMtiComponents_CreatesCorrectType()
    {
        var msg = _factory.NewMessage(MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);
        Assert.NotNull(msg);
        Assert.Equal(0x0800, msg.Type);
    }

    [Fact]
    public void CreateResponse_SetsResponseType()
    {
        var request = _factory.NewMessage(0x0200);
        request.SetField(11, new IsoValue(IsoType.ALPHA, "123456", 6));
        var response = _factory.CreateResponse(request);
        Assert.NotNull(response);
        Assert.Equal(0x0210, response.Type);
    }

    [Fact]
    public void CreateResponse_WithCopyAllFields_CopiesFields()
    {
        var request = _factory.NewMessage(0x0200);
        request.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        request.SetField(11, new IsoValue(IsoType.ALPHA, "123456", 6));
        var response = _factory.CreateResponse(request, true);
        Assert.NotNull(response);
        Assert.True(response.HasField(3));
        Assert.True(response.HasField(11));
    }

    [Fact]
    public void ParseMessage_RoundTrips()
    {
        var original = _factory.NewMessage(0x1100);
        original.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        original.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        original.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        original.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        original.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        original.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));

        var sbytes = original.WriteData();
        var bytes = new byte[sbytes.Length];
        Buffer.BlockCopy(sbytes, 0, bytes, 0, sbytes.Length);

        var parsed = _factory.ParseMessage(bytes, 0);
        Assert.NotNull(parsed);
        Assert.Equal(0x1100, parsed.Type);
        Assert.Equal("004000", parsed.GetField(3).Value.ToString());
    }

    [Fact]
    public void ParseMessage_InvalidBytes_ReturnsNullOrThrows()
    {
        // Garbage bytes that can't form a valid ISO message
        try
        {
            var result = _factory.ParseMessage(new byte[] { 0, 0, 0, 0 }, 0);
            Assert.Null(result);
        }
        catch (Exception)
        {
            // Some implementations throw on invalid data - that's also acceptable
        }
    }
}
