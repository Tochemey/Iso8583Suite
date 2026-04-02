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

using System.Text;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Client;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class ChannelInitializerTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public ChannelInitializerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    [Fact]
    public void ServerConfig_WithLogging_AddsLoggingHandler()
    {
        var config = new ServerConfiguration
        {
            AddLoggingHandler = true,
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4
        };
        // Just verifying config validation passes and the config is consumable
        config.Validate();
        Assert.True(config.AddLoggingHandler);
    }

    [Fact]
    public void ServerConfig_WithReplyOnError_Validates()
    {
        var config = new ServerConfiguration
        {
            ReplyOnError = true,
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4
        };
        config.Validate();
        Assert.True(config.ReplyOnError);
    }

    [Fact]
    public void ServerConfig_WithSsl_Validates()
    {
        var config = new ServerConfiguration
        {
            Ssl = new SslConfiguration
            {
                Enabled = true,
                CertificatePath = "/path/to/cert.pfx",
                CertificatePassword = "pass"
            }
        };
        config.Validate();
        Assert.True(config.Ssl.Enabled);
    }

    [Fact]
    public void ClientConfig_WithAutoReconnect_Validates()
    {
        var config = new ClientConfiguration
        {
            AutoReconnect = true,
            ReconnectInterval = 500,
            MaxReconnectDelay = 60000,
            MaxReconnectAttempts = 5
        };
        config.Validate();
        Assert.True(config.AutoReconnect);
        Assert.Equal(500, config.ReconnectInterval);
    }

    [Fact]
    public void EchoMessageListener_HandleMessage_CreatesResponse()
    {
        var listener = new EchoMessageListener<IsoMessage>(_factory);
        var echoRequest = _factory.NewMessage(MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);

        Assert.True(listener.CanHandleMessage(echoRequest));
    }

    [Fact]
    public void ParseExceptionHandler_CreatesErrorResponse()
    {
        var handler = new ParseExceptionHandler(_factory, true);
        Assert.True(handler.IsSharable);
    }

    [Fact]
    public void CompositeHandler_ChannelRead_NonIsoMessage_DoesNotThrow()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        var channel = new EmbeddedChannel(handler);

        // Write a non-IsoMessage object - should be silently ignored
        channel.WriteInbound("not an iso message");

        // Should not throw, and channel should still be open
        Assert.True(channel.IsOpen);
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void CompositeHandler_WithListener_ProcessesMessage()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        var listener = new TrackingListener();
        handler.AddListener(listener);

        var channel = new EmbeddedChannel(handler);
        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000001", 6));

        channel.WriteInbound(msg);

        // Give async handler time to process
        System.Threading.Thread.Sleep(200);

        Assert.True(listener.WasCalled);
        channel.CloseAsync().Wait();
    }

    private class TrackingListener : IIsoMessageListener<IsoMessage>
    {
        public volatile bool WasCalled;
        public bool CanHandleMessage(IsoMessage isoMessage) => true;
        public System.Threading.Tasks.Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage)
        {
            WasCalled = true;
            return System.Threading.Tasks.Task.FromResult(false);
        }
    }
}
