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
using System.Collections.Concurrent;
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
///   End-to-end scenario integration tests. Each test brings up a real server, connects a real
///   client, and verifies a full request/response flow over a loopback TCP socket. The scenarios
///   mirror the flows demonstrated in <c>SampleServer</c> and <c>SampleClient</c> so this file
///   doubles as a reference for building ISO 8583 integrations.
/// </summary>
[Collection(nameof(TcpServerCollection))]
public class ScenarioTests : IAsyncLifetime
{
    private Iso8583Server<IsoMessage> _server = null!;
    private Iso8583Client<IsoMessage> _client = null!;
    private IsoMessageFactory<IsoMessage> _factory = null!;
    private readonly int _port = TestPorts.Next();

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
        _server = new Iso8583Server<IsoMessage>(_port, serverConfig, _factory);
        _server.AddMessageListener(new NetworkManagementListener(_factory));
        _server.AddMessageListener(new PurchaseAuthorizationListener(_factory));
        _server.AddMessageListener(new ReversalListener(_factory));
        await _server.Start();
        await Task.Delay(200);

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false
        };
        _client = new Iso8583Client<IsoMessage>(clientConfig, _factory);
        await _client.Connect("127.0.0.1", _port);
    }

    public async Task DisposeAsync()
    {
        try { await _client.Disconnect(); } catch { /* ignore */ }
        try { await _server.Shutdown(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
    }

    // ---------------- Network management (0800 → 0810) ----------------

    [Fact]
    public async Task NetworkManagement_SignOn_GetsApprovalResponse()
    {
        var request = _factory.NewMessage(0x0800);
        request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        request.SetField(11, new IsoValue(IsoType.NUMERIC, "000001", 6));
        request.SetField(70, new IsoValue(IsoType.NUMERIC, "001", 3));

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0810, response.Type);
        Assert.Equal("001", response.GetField(70)?.Value?.ToString());
    }

    [Fact]
    public async Task NetworkManagement_Echo_GetsEchoResponse()
    {
        var request = _factory.NewMessage(0x0800);
        request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        request.SetField(11, new IsoValue(IsoType.NUMERIC, "000002", 6));
        request.SetField(70, new IsoValue(IsoType.NUMERIC, "301", 3));

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0810, response.Type);
        Assert.Equal("301", response.GetField(70)?.Value?.ToString());
    }

    [Fact]
    public async Task NetworkManagement_SignOff_GetsApprovalResponse()
    {
        var request = _factory.NewMessage(0x0800);
        request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        request.SetField(11, new IsoValue(IsoType.NUMERIC, "000003", 6));
        request.SetField(70, new IsoValue(IsoType.NUMERIC, "002", 3));

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0810, response.Type);
        Assert.Equal("002", response.GetField(70)?.Value?.ToString());
    }

    // ---------------- Purchase authorization (0200 → 0210) ----------------

    [Fact]
    public async Task Purchase_AmountBelowLimit_Approved()
    {
        var request = BuildPurchase("000100", amountMinorUnits: 12345);

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0210, response.Type);
        Assert.Equal("00", response.GetField(39)?.Value?.ToString());
    }

    [Fact]
    public async Task Purchase_AmountAboveLimit_Declined()
    {
        var request = BuildPurchase("000101", amountMinorUnits: 99999);

        var response = await _client.SendAndReceive(request, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0210, response.Type);
        Assert.Equal("61", response.GetField(39)?.Value?.ToString());
    }

    // ---------------- Reversal flow (0420 → 0430) ----------------

    [Fact]
    public async Task Reversal_AfterDeclinedPurchase_Accepted()
    {
        // Step 1: send a purchase that will be declined.
        var originalStan = "000200";
        var purchase = BuildPurchase(originalStan, amountMinorUnits: 99999);
        var purchaseResponse = await _client.SendAndReceive(purchase, TimeSpan.FromSeconds(5));
        Assert.Equal("61", purchaseResponse.GetField(39)?.Value?.ToString());

        // Step 2: reverse it by copying the key fields into a 0420 message.
        var reversal = _factory.NewMessage(0x0420);
        reversal.SetField(3, purchase.GetField(3));
        reversal.SetField(4, purchase.GetField(4));
        reversal.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        reversal.SetField(11, new IsoValue(IsoType.NUMERIC, "000201", 6));
        reversal.SetField(37, purchase.GetField(37));
        reversal.SetField(41, purchase.GetField(41));
        reversal.SetField(49, purchase.GetField(49));

        var reversalResponse = await _client.SendAndReceive(reversal, TimeSpan.FromSeconds(5));

        Assert.Equal(0x0430, reversalResponse.Type);
        Assert.Equal("00", reversalResponse.GetField(39)?.Value?.ToString());
    }

    [Fact]
    public async Task Reversal_Duplicate_StillAcknowledged()
    {
        // Idempotent reversal handling: replaying the same reversal STAN must still succeed.
        var purchase = BuildPurchase("000300", amountMinorUnits: 99999);
        _ = await _client.SendAndReceive(purchase, TimeSpan.FromSeconds(5));

        var reversal = BuildReversal(purchase, reversalStan: "000301");
        var first = await _client.SendAndReceive(reversal, TimeSpan.FromSeconds(5));
        Assert.Equal("00", first.GetField(39)?.Value?.ToString());

        var replay = BuildReversal(purchase, reversalStan: "000302");
        var second = await _client.SendAndReceive(replay, TimeSpan.FromSeconds(5));
        Assert.Equal("00", second.GetField(39)?.Value?.ToString());
    }

    // ---------------- Helpers ----------------

    private IsoMessage BuildPurchase(string stan, long amountMinorUnits)
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "000000", 6));
        msg.SetField(4, new IsoValue(IsoType.AMOUNT, amountMinorUnits));
        msg.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        msg.SetField(11, new IsoValue(IsoType.NUMERIC, stan, 6));
        msg.SetField(12, new IsoValue(IsoType.TIME, DateTime.UtcNow));
        msg.SetField(13, new IsoValue(IsoType.DATE4, DateTime.UtcNow));
        msg.SetField(37, new IsoValue(IsoType.NUMERIC, "100000000001", 12));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "TRM00001", 16));
        msg.SetField(49, new IsoValue(IsoType.ALPHA, "840", 3));
        return msg;
    }

    private IsoMessage BuildReversal(IsoMessage original, string reversalStan)
    {
        var reversal = _factory.NewMessage(0x0420);
        reversal.SetField(3, original.GetField(3));
        reversal.SetField(4, original.GetField(4));
        reversal.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        reversal.SetField(11, new IsoValue(IsoType.NUMERIC, reversalStan, 6));
        reversal.SetField(37, original.GetField(37));
        reversal.SetField(41, original.GetField(41));
        reversal.SetField(49, original.GetField(49));
        return reversal;
    }

    // ---------------- Scenario listeners (mirror SampleServer) ----------------

    private class NetworkManagementListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public NetworkManagementListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0800;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var response = _factory.CreateResponse(request);
            response.CopyFieldsFrom(request, 7, 11, 70);
            response.SetField(39, new IsoValue(IsoType.ALPHA, "-1", 2));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }

    private class PurchaseAuthorizationListener : IIsoMessageListener<IsoMessage>
    {
        private const long ApprovalLimitMinorUnits = 50000;
        private readonly IsoMessageFactory<IsoMessage> _factory;
        public PurchaseAuthorizationListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0200;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            _ = long.TryParse(request.GetField(4)?.Value?.ToString(), out var amount);
            var responseCode = amount <= ApprovalLimitMinorUnits ? "00" : "61";

            var response = _factory.CreateResponse(request);
            response.CopyFieldsFrom(request, 3, 4, 7, 11, 12, 37, 41, 49);
            response.RemoveFields(13, 14, 19, 22, 24, 26, 35, 45);
            response.SetField(38, new IsoValue(IsoType.ALPHA, "AUTH01", 6));
            response.SetField(39, new IsoValue(IsoType.NUMERIC, responseCode, 2));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }

    private class ReversalListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;
        private readonly ConcurrentDictionary<string, byte> _reversedStans = new();

        public ReversalListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;
        public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0420;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
        {
            var originalStan = request.GetField(11)?.Value?.ToString() ?? string.Empty;
            _reversedStans.TryAdd(originalStan, 0);

            var response = _factory.CreateResponse(request);
            response.CopyFieldsFrom(request, 3, 4, 7, 11, 37, 41, 49);
            response.SetField(39, new IsoValue(IsoType.NUMERIC, "00", 2));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
