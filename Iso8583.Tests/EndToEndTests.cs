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
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Client;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class EndToEndTests : IAsyncLifetime
{
    private Iso8583Server<IsoMessage> _server = null!;
    private Iso8583Client<IsoMessage> _client = null!;
    private IsoMessageFactory<IsoMessage> _factory = null!;
    private readonly int Port = TestPorts.Next();

    public async Task InitializeAsync()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var serverConfig = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60
        };
        _server = new Iso8583Server<IsoMessage>(Port, serverConfig, _factory);
        _server.AddMessageListener(new EchoBackListener(_factory));
        await _server.Start();

        // Allow the server accept loop to fully start (needed on macOS CI)
        await Task.Delay(200);

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false
        };
        _client = new Iso8583Client<IsoMessage>(clientConfig, _factory);
        await _client.Connect("127.0.0.1", Port);
    }

    public async Task DisposeAsync()
    {
        try { await _client.Disconnect(); } catch { /* ignore */ }
        try { await _server.Shutdown(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
    }

    [Fact]
    public void Server_IsStarted()
    {
        Assert.True(_server.IsStarted());
    }

    [Fact]
    public void Client_IsConnected()
    {
        Assert.True(_client.IsConnected());
    }

    [Fact]
    public async Task SendAndReceive_AuthorizationRequest_GetsResponse()
    {
        var request = CreateAuthRequest("000001");

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.NotNull(response);
        Assert.Equal(0x1110, response.Type);
        Assert.Equal("000", response.GetField(39)?.Value?.ToString());
    }

    [Fact]
    public async Task Send_FireAndForget_DoesNotThrow()
    {
        var request = CreateAuthRequest("000002");
        await _client.Send(request);
        // Give the server time to process
        await Task.Delay(200);
    }

    [Fact]
    public async Task SendAndReceive_MultipleRequests_AllCorrelateCorrectly()
    {
        var tasks = new Task<IsoMessage>[5];
        for (var i = 0; i < 5; i++)
        {
            var stan = $"00{i:D4}";
            var request = CreateAuthRequest(stan);
            tasks[i] = _client.SendAndReceive(request, TimeSpan.FromSeconds(5));
        }

        var responses = await Task.WhenAll(tasks);

        foreach (var response in responses)
        {
            Assert.NotNull(response);
            Assert.Equal(0x1110, response.Type);
        }
    }

    [Fact]
    public async Task Client_SendNotConnected_ThrowsInvalidOperation()
    {
        var disconnectedClient = new Iso8583Client<IsoMessage>(
            new ClientConfiguration
            {
                EncodeFrameLengthAsString = true,
                FrameLengthFieldLength = 4,
                AutoReconnect = false
            }, _factory);

        // Never connected - should throw
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await disconnectedClient.Send(_factory.NewMessage(0x1100)));
    }

    [Fact]
    public async Task Server_ActiveConnectionCount_IsOne()
    {
        // Allow time for ChannelActive to fire on the server pipeline
        for (var i = 0; i < 50 && _server.ActiveConnectionCount == 0; i++)
            await Task.Delay(100);

        Assert.Equal(1, _server.ActiveConnectionCount);
    }

    [Fact]
    public async Task Client_Disconnect_ThenSend_ThrowsInvalidOperation()
    {
        var separateClient = new Iso8583Client<IsoMessage>(
            new ClientConfiguration
            {
                EncodeFrameLengthAsString = true,
                FrameLengthFieldLength = 4,
                AutoReconnect = false
            }, _factory);

        await separateClient.Connect("127.0.0.1", Port);
        await separateClient.Disconnect();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await separateClient.Send(_factory.NewMessage(0x1100)));
    }

    [Fact]
    public async Task Client_DisposeAsync_ThenSend_ThrowsObjectDisposed()
    {
        var separateClient = new Iso8583Client<IsoMessage>(
            new ClientConfiguration
            {
                EncodeFrameLengthAsString = true,
                FrameLengthFieldLength = 4,
                AutoReconnect = false
            }, _factory);

        await separateClient.Connect("127.0.0.1", Port);
        await separateClient.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await separateClient.Send(_factory.NewMessage(0x1100)));
    }

    private IsoMessage CreateAuthRequest(string stan)
    {
        var msg = _factory.NewMessage(0x1100);
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        msg.SetField(11, new IsoValue(IsoType.ALPHA, stan, 6));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        msg.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        return msg;
    }

    private class EchoBackListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public EchoBackListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => true;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var response = _factory.CreateResponse(request);
            // Ensure STAN (field 11) is copied for correlation
            if (request.HasField(11))
                response.SetField(11, request.GetField(11));
            response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
