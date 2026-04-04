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
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using Iso8583.Common.Validation;
using Iso8583.Common.Validation.Validators;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class MessageValidationHandlerTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public MessageValidationHandlerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    private IsoMessage ValidMessage()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111111", 16));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000001000", 12));
        msg.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));
        return msg;
    }

    private IsoMessage InvalidPanMessage()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111112", 16)); // bad Luhn
        return msg;
    }

    private static MessageValidator LuhnOnField2()
    {
        var v = new MessageValidator();
        v.ForField(2).AddRule(new LuhnValidator());
        return v;
    }

    /// <summary>
    ///   Write a single outbound message and flush. In SpanNetty's EmbeddedChannel, the write
    ///   promise only completes once the pipeline is flushed, so tests must flush explicitly.
    /// </summary>
    private static async Task WriteAndFlushAsync(EmbeddedChannel channel, object message)
    {
        var task = channel.WriteOneOutbound(message);
        channel.FlushOutbound();
        await task;
    }

    // ---------- Pass-through when validator is null ----------

    [Fact]
    public void NullValidator_InboundMessage_PassesThrough()
    {
        var handler = new MessageValidationHandler(null);
        var channel = new EmbeddedChannel(handler);

        channel.WriteInbound(InvalidPanMessage());

        var received = channel.ReadInbound<IsoMessage>();
        Assert.NotNull(received);
        Assert.Equal(0x0200, received.Type);
    }

    [Fact]
    public async Task NullValidator_OutboundMessage_PassesThrough()
    {
        var handler = new MessageValidationHandler(null);
        var channel = new EmbeddedChannel(handler);

        await WriteAndFlushAsync(channel, InvalidPanMessage());

        var written = channel.ReadOutbound<IsoMessage>();
        Assert.NotNull(written);
    }

    [Fact]
    public void NullValidator_NonIsoInbound_PassesThrough()
    {
        var handler = new MessageValidationHandler(null);
        var channel = new EmbeddedChannel(handler);

        channel.WriteInbound("hello");

        var received = channel.ReadInbound<string>();
        Assert.Equal("hello", received);
    }

    [Fact]
    public async Task NullValidator_NonIsoOutbound_PassesThrough()
    {
        var handler = new MessageValidationHandler(null);
        var channel = new EmbeddedChannel(handler);

        await WriteAndFlushAsync(channel, "hello");

        var written = channel.ReadOutbound<string>();
        Assert.Equal("hello", written);
    }

    // ---------- Inbound validation ----------

    [Fact]
    public void Inbound_ValidMessage_PassesThrough()
    {
        var handler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(handler);

        channel.WriteInbound(ValidMessage());

        var received = channel.ReadInbound<IsoMessage>();
        Assert.NotNull(received);
    }

    [Fact]
    public void Inbound_InvalidMessage_FiresExceptionAndDropsMessage()
    {
        Exception captured = null;
        var exceptionCatcher = new TestExceptionCatcher(e => captured = e);
        var validationHandler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(validationHandler, exceptionCatcher);

        channel.WriteInbound(InvalidPanMessage());

        var received = channel.ReadInbound<IsoMessage>();
        Assert.Null(received);

        Assert.NotNull(captured);
        var validationEx = Assert.IsType<MessageValidationException>(captured);
        Assert.False(validationEx.Report.IsValid);
        Assert.Contains(validationEx.Report.Errors, e => e.FieldNumber == 2);
    }

    [Fact]
    public void Inbound_NonIsoMessage_PassesThrough()
    {
        var handler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(handler);

        channel.WriteInbound("raw text");

        var received = channel.ReadInbound<string>();
        Assert.Equal("raw text", received);
    }

    // ---------- Outbound validation ----------

    [Fact]
    public async Task Outbound_ValidMessage_Written()
    {
        var handler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(handler);

        await WriteAndFlushAsync(channel, ValidMessage());

        var written = channel.ReadOutbound<IsoMessage>();
        Assert.NotNull(written);
    }

    [Fact]
    public async Task Outbound_InvalidMessage_FailsWriteAndBlocksWire()
    {
        var handler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(handler);

        var ex = await Assert.ThrowsAsync<MessageValidationException>(
            () => WriteAndFlushAsync(channel, InvalidPanMessage()));

        Assert.False(ex.Report.IsValid);
        Assert.Contains(ex.Report.Errors, e => e.FieldNumber == 2);

        var written = channel.ReadOutbound<IsoMessage>();
        Assert.Null(written);
    }

    [Fact]
    public async Task Outbound_NonIsoMessage_PassesThrough()
    {
        var handler = new MessageValidationHandler(LuhnOnField2());
        var channel = new EmbeddedChannel(handler);

        await WriteAndFlushAsync(channel, "raw text");

        var written = channel.ReadOutbound<string>();
        Assert.Equal("raw text", written);
    }

    // ---------- Sharable flag ----------

    [Fact]
    public void Handler_IsSharable()
    {
        var handler = new MessageValidationHandler(null);
        Assert.True(handler.IsSharable);
    }

    // ---------- Test helpers ----------

    private sealed class TestExceptionCatcher : ChannelHandlerAdapter
    {
        private readonly Action<Exception> _onException;

        public TestExceptionCatcher(Action<Exception> onException)
        {
            _onException = onException;
        }

        public override bool IsSharable => true;

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            _onException(exception);
            // Do not propagate further so the EmbeddedChannel does not rethrow.
        }
    }
}
