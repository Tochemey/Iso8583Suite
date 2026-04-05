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
using BenchmarkDotNet.Attributes;
using DotNetty.Transport.Channels;
using Iso8583.Client;
using Iso8583.Common;
using NetCore8583;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for the PendingRequestManager: registration, matching, and key building.
/// </summary>
[MemoryDiagnoser]
public class CorrelationBenchmarks
{
    private PendingRequestManagerAccessor _manager;
    private IsoMessage _request;
    private IsoMessage _response;

    [GlobalSetup]
    public void Setup()
    {
        _manager = new PendingRequestManagerAccessor();

        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        _request = mfact.NewMessage(0x0200);
        _request.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));

        _response = mfact.NewMessage(0x0210);
        _response.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
    }

    [Benchmark(Description = "Register + Complete pending request cycle")]
    public async Task RegisterAndComplete()
    {
        var (_, responseTask) = _manager.RegisterPending(_request, TimeSpan.FromSeconds(5));

        _manager.CanHandleMessage(_response);
        await _manager.HandleMessage(null, _response);

        await responseTask;
    }

    /// <summary>
    ///   Accessor to expose the internal PendingRequestManager for benchmarking.
    /// </summary>
    internal class PendingRequestManagerAccessor : IIsoMessageListener<IsoMessage>
    {
        private readonly PendingRequestManager<IsoMessage> _inner = new();

        public (string key, Task<IsoMessage> task) RegisterPending(IsoMessage msg, TimeSpan timeout) =>
            _inner.RegisterPending(msg, timeout);

        public bool CanHandleMessage(IsoMessage isoMessage) => _inner.CanHandleMessage(isoMessage);

        public Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage) =>
            _inner.HandleMessage(context, isoMessage);
    }
}
