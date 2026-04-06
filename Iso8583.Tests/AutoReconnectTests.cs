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
using System.Linq;
using System.Text;
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

/// <summary>
///   Verifies that <see cref="Iso8583Client{T}"/> auto-reconnects after the server drops the
///   connection. Runs in its own class (outside <see cref="ScenarioTests"/>) to avoid sharing
///   the ScenarioTests fixture, whose persistent server and event loop groups can interfere
///   with the reconnect handler's new bootstrap.
/// </summary>
[Collection(nameof(TcpServerCollection))]
public class AutoReconnectTests
{
    [Fact(Timeout = 60_000)]
    public async Task AutoReconnect_AfterConnectionDrop_ClientReconnectsAndCanSendMessages()
    {
        var port = TestPorts.Next();

        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        var factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var serverConfig = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60
        };

        var server = new Iso8583Server<IsoMessage>(port, serverConfig, factory);
        server.AddMessageListener(new EchoListener(factory));
        await server.Start();
        await Task.Delay(200);

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = true,
            ReconnectInterval = 200,
            MaxReconnectDelay = 2000,
            MaxReconnectAttempts = 10
        };
        var client = new Iso8583Client<IsoMessage>(clientConfig, factory);
        await client.Connect("127.0.0.1", port);
        Assert.True(client.IsConnected());

        // Step 1: verify the connection works.
        var resp1 = await client.SendAndReceive(
            BuildEcho(factory, "000001"), TimeSpan.FromSeconds(5));
        Assert.Equal(0x0810, resp1.Type);

        // Step 2: server drops the client connection (simulates network interruption
        // or remote-end restart). The server keeps listening so the client's
        // auto-reconnect can succeed after the backoff delay.
        foreach (var conn in server.ActiveConnections.ToList())
        {
            try { await conn.CloseAsync(); } catch { /* ignore */ }
        }

        // Step 3: wait until a send succeeds. The reconnect handler uses exponential
        // backoff, so the channel may briefly report IsActive=true before the pipeline
        // is fully operational. Retrying the actual send is the most reliable proof
        // that the reconnected session works end-to-end.
        IsoMessage resp2 = null;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (resp2 == null && DateTime.UtcNow < deadline)
        {
            if (!client.IsConnected())
            {
                await Task.Delay(200);
                continue;
            }
            try
            {
                resp2 = await client.SendAndReceive(
                    BuildEcho(factory, "000002"), TimeSpan.FromSeconds(3));
            }
            catch
            {
                await Task.Delay(500);
            }
        }

        Assert.NotNull(resp2);
        Assert.Equal(0x0810, resp2!.Type);

        // Cleanup: disconnect client first to suppress further reconnect attempts.
        try { await client.Disconnect(); } catch { /* ignore */ }
        await Task.Delay(200);
        try { await server.Shutdown(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
    }

    private static IsoMessage BuildEcho(IsoMessageFactory<IsoMessage> factory, string stan)
    {
        var msg = factory.NewMessage(0x0800);
        msg.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        msg.SetField(11, new IsoValue(IsoType.NUMERIC, stan, 6));
        msg.SetField(70, new IsoValue(IsoType.NUMERIC, "301", 3));
        return msg;
    }

    private class EchoListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public EchoListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => true;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var response = _factory.CreateResponse(request);
            if (request.HasField(11))
                response.SetField(11, request.GetField(11));
            if (request.HasField(70))
                response.SetField(70, request.GetField(70));
            response.SetField(39, new IsoValue(IsoType.ALPHA, "-1", 2));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
