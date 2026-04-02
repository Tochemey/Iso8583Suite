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
using BenchmarkDotNet.Attributes;
using DotNetty.Transport.Channels;
using Iso8583.Client;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   End-to-end benchmark: measures the full round-trip latency for a client sending
///   a request to an in-process server and receiving a correlated response.
/// </summary>
[MemoryDiagnoser]
public class EndToEndBenchmarks
{
    private Iso8583Server<IsoMessage> _server;
    private Iso8583Client<IsoMessage> _client;
    private IsoMessageFactory<IsoMessage> _messageFactory;
    private const int Port = 19876;

    [GlobalSetup]
    public async Task Setup()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var serverConfig = new ServerConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60
        };

        _server = new Iso8583Server<IsoMessage>(Port, serverConfig, _messageFactory);
        _server.AddMessageListener(new EchoBackListener(_messageFactory));
        await _server.Start();

        var clientConfig = new ClientConfiguration
        {
            EncodeFrameLengthAsString = true,
            FrameLengthFieldLength = 4,
            IdleTimeout = 60,
            AutoReconnect = false
        };

        _client = new Iso8583Client<IsoMessage>(clientConfig, _messageFactory);
        await _client.Connect("127.0.0.1", Port);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _client.Disconnect();
        await _server.Shutdown(TimeSpan.FromSeconds(1));
    }

    [Benchmark(Description = "Full round-trip: SendAndReceive (authorization request/response)")]
    public async Task<IsoMessage> RoundTrip()
    {
        var msg = _messageFactory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        msg.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        msg.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        msg.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));

        return await _client.SendAndReceive(msg, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    ///   Simple listener that creates a response for any financial message and sends it back.
    /// </summary>
    private class EchoBackListener : IIsoMessageListener<IsoMessage>
    {
        private readonly IsoMessageFactory<IsoMessage> _factory;

        public EchoBackListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;

        public bool CanHandleMessage(IsoMessage isoMessage) => true;

        public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage)
        {
            var response = _factory.CreateResponse(isoMessage);
            response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
            await context.WriteAndFlushAsync(response);
            return false;
        }
    }
}
