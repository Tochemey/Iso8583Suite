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
using System.Threading.Tasks;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class EchoMessageListenerTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public EchoMessageListenerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    [Fact]
    public void CanHandleMessage_NetworkManagement_ReturnsTrue()
    {
        var listener = new EchoMessageListener<IsoMessage>(_factory);
        var msg = _factory.NewMessage(MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);
        Assert.True(listener.CanHandleMessage(msg));
    }

    [Fact]
    public void CanHandleMessage_NonNetworkManagement_ReturnsFalse()
    {
        var listener = new EchoMessageListener<IsoMessage>(_factory);
        var msg = _factory.NewMessage(0x0200);
        Assert.False(listener.CanHandleMessage(msg));
    }
}

public class ParseExceptionHandlerTests
{
    [Fact]
    public void IsSharable_ReturnsTrue()
    {
        var mfact = ConfigParser.CreateDefault();
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        var factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var handler = new ParseExceptionHandler(factory, true);
        Assert.True(handler.IsSharable);
    }
}

public class IsoMessageLoggingHandlerTests
{
    private readonly IsoMessage _message;
    private readonly IsoMessageLoggingHandler _handlerSensitive;
    private readonly IsoMessageLoggingHandler _handlerMasked;

    public IsoMessageLoggingHandlerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        _message = mfact.NewMessage(0x0200);
        _message.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        _message.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        _message.SetField(35, new IsoValue(IsoType.LLVAR, "5164123785712481D17021011408011015360", 37));
        _message.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        _handlerSensitive = new IsoMessageLoggingHandler(LogLevel.DEBUG, printSensitiveData: true, printFieldDescriptions: true);
        _handlerMasked = new IsoMessageLoggingHandler(LogLevel.DEBUG, printSensitiveData: false, printFieldDescriptions: true);
    }

    [Fact]
    public void IsSharable_ReturnsTrue()
    {
        Assert.True(_handlerSensitive.IsSharable);
    }

    [Fact]
    public void FormatIsoMessage_WithSensitiveData_ContainsPan()
    {
        var result = _handlerSensitive.FormatIsoMessage(_message);
        Assert.Contains("5164123785712481", result);
        Assert.Contains("0x0200", result);
    }

    [Fact]
    public void FormatIsoMessage_WithMasking_MasksPan()
    {
        var result = _handlerMasked.FormatIsoMessage(_message);
        Assert.Contains("516412******2481", result);
        Assert.Contains("0x0200", result);
    }

    [Fact]
    public void FormatIsoMessage_WithMasking_MasksTrack2()
    {
        var result = _handlerMasked.FormatIsoMessage(_message);
        Assert.Contains("***", result);
    }

    [Fact]
    public void FormatIsoMessage_ContainsFieldType()
    {
        var result = _handlerSensitive.FormatIsoMessage(_message);
        Assert.Contains("LLVAR", result);
    }
}

public class IdleEventHandlerTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var mfact = ConfigParser.CreateDefault();
        mfact.UseBinaryMessages = false;
        var factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
        var handler = new IdleEventHandler(factory);
        Assert.NotNull(handler);
    }
}
