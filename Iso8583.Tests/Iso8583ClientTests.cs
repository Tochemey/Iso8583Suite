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

public class Iso8583ClientTests : IAsyncLifetime
{
    private Iso8583Server<IsoMessage> _server = null!;
    private IsoMessageFactory<IsoMessage> _factory = null!;
    private static readonly int Port = TestPorts.Next();

    public async Task InitializeAsync()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var config = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60
        };
        _server = new Iso8583Server<IsoMessage>(Port, config, _factory);
        _server.AddMessageListener(new EchoBackListener(_factory));
        await _server.Start();
    }

    public async Task DisposeAsync()
    {
        try { await _server.Shutdown(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
    }

    private Iso8583Client<IsoMessage> CreateClient()
    {
        return new Iso8583Client<IsoMessage>(new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false
        }, _factory);
    }

    [Fact]
    public async Task Send_WithTimeout_Succeeds()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000001", 6));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        await client.Send(msg, 5000); // should not throw
        await client.Disconnect();
    }

    [Fact]
    public async Task IsConnected_BeforeConnect_ReturnsFalse()
    {
        var client = CreateClient();
        Assert.False(client.IsConnected());
    }

    [Fact]
    public async Task IsConnected_AfterConnect_ReturnsTrue()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);
        Assert.True(client.IsConnected());
        await client.Disconnect();
    }

    [Fact]
    public async Task IsConnected_AfterDisconnect_ReturnsFalse()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);
        await client.Disconnect();
        Assert.False(client.IsConnected());
    }

    [Fact]
    public async Task SendAndReceive_WithCancellation_ThrowsWhenCancelled()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000099", 6));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.SendAndReceive(msg, TimeSpan.FromSeconds(5), cts.Token));

        await client.Disconnect();
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);
        await client.DisposeAsync();
        await client.DisposeAsync(); // second call should be safe
    }

    [Fact]
    public async Task Connect_AfterDispose_ThrowsObjectDisposed()
    {
        var client = CreateClient();
        await client.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.Connect("127.0.0.1", Port));
    }

    [Fact]
    public async Task Connect_ByIpString_Succeeds()
    {
        var client = CreateClient();
        await client.Connect("127.0.0.1", Port);
        Assert.True(client.IsConnected());
        await client.Disconnect();
    }

    private class EchoBackListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public EchoBackListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => true;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var response = _factory.CreateResponse(request);
            if (request.HasField(11))
                response.SetField(11, request.GetField(11));
            response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
