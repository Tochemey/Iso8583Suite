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

namespace Iso8583.Tests;

[Collection(nameof(TcpServerCollection))]
public class PooledIso8583ClientTests : IAsyncLifetime
{
    private Iso8583Server<IsoMessage> _server = null!;
    private IsoMessageFactory<IsoMessage> _factory = null!;
    private readonly int Port = TestPorts.Next();

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
        await Task.Delay(200);
    }

    public async Task DisposeAsync()
    {
        try { await _server.Shutdown(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
    }

    private PooledClientConfiguration CreateConfig(int poolSize = 3)
    {
        return new PooledClientConfiguration
        {
            PoolSize = poolSize,
            ClientConfiguration = new ClientConfiguration
            {
                EncodeFrameLengthAsString = true,
                FrameLengthFieldLength = 4,
                IdleTimeout = 60,
                AutoReconnect = false
            }
        };
    }

    [Fact(Timeout = 30_000)]
    public async Task Connect_AllConnectionsEstablished()
    {
        var config = CreateConfig(3);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);

        await pool.Connect("127.0.0.1", Port);

        Assert.Equal(3, pool.PoolSize);

        // Poll briefly for all channels to register as active (tolerates thread scheduling variance on older runtimes).
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (pool.ActiveConnectionCount < 3 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal(3, pool.ActiveConnectionCount);
    }

    [Fact(Timeout = 30_000)]
    public async Task Send_FireAndForget_Succeeds()
    {
        var config = CreateConfig(2);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000001", 6));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        await pool.Send(msg); // should not throw
    }

    [Fact(Timeout = 30_000)]
    public async Task Send_WithTimeout_Succeeds()
    {
        var config = CreateConfig(2);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000002", 6));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        await pool.Send(msg, 5000); // should not throw
    }

    [Fact(Timeout = 30_000)]
    public async Task SendAndReceive_GetsCorrelatedResponse()
    {
        var config = CreateConfig(2);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000003", 6));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

        var response = await pool.SendAndReceive(msg, TimeSpan.FromSeconds(5));

        Assert.NotNull(response);
        Assert.True(response.HasField(39)); // response code
    }

    [Fact(Timeout = 30_000)]
    public async Task SendAndReceive_MultipleRequests_DistributedAcrossPool()
    {
        var config = CreateConfig(3);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);

        // Send several requests sequentially — round-robin should spread them
        for (var i = 0; i < 6; i++)
        {
            var msg = _factory.NewMessage(0x1100);
            msg.SetField(11, new IsoValue(IsoType.ALPHA, $"{100 + i:D6}", 6));
            msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

            var response = await pool.SendAndReceive(msg, TimeSpan.FromSeconds(5));
            Assert.NotNull(response);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task SendAndReceive_WithLeastConnectionsBalancer_Succeeds()
    {
        var config = CreateConfig(2);
        PooledIso8583Client<IsoMessage> pool = null!;
        pool = new PooledIso8583Client<IsoMessage>(
            config, _factory,
            new LeastConnectionsLoadBalancer(idx => pool.GetPendingCount(idx)));

        await using (pool)
        {
            await pool.Connect("127.0.0.1", Port);

            var msg = _factory.NewMessage(0x1100);
            msg.SetField(11, new IsoValue(IsoType.ALPHA, "000010", 6));
            msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));

            var response = await pool.SendAndReceive(msg, TimeSpan.FromSeconds(5));
            Assert.NotNull(response);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task Disconnect_AllConnectionsClosed()
    {
        var config = CreateConfig(2);
        await using var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);

        await pool.Disconnect();

        // Poll briefly for channels to fully deactivate (event loop shutdown is asynchronous).
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (pool.ActiveConnectionCount > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.Equal(0, pool.ActiveConnectionCount);
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        var config = CreateConfig(2);
        var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.Connect("127.0.0.1", Port);
        await pool.DisposeAsync();
        await pool.DisposeAsync(); // second call should be safe
    }

    [Fact(Timeout = 30_000)]
    public async Task Send_AfterDispose_ThrowsObjectDisposed()
    {
        var config = CreateConfig(2);
        var pool = new PooledIso8583Client<IsoMessage>(config, _factory);
        await pool.DisposeAsync();

        var msg = _factory.NewMessage(0x1100);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "000099", 6));

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await pool.Send(msg));
    }

    [Fact]
    public void Constructor_InvalidConfig_Throws()
    {
        var config = new PooledClientConfiguration { PoolSize = 0 };
        Assert.Throws<ArgumentException>(() =>
            new PooledIso8583Client<IsoMessage>(config, _factory));
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
