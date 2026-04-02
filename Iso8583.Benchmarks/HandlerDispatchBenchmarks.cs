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

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for the CompositeIsoMessageHandler copy-on-write listener snapshot
///   and PendingRequestManager correlation lookup.
/// </summary>
[MemoryDiagnoser]
public class HandlerDispatchBenchmarks
{
    private CompositeIsoMessageHandler<IsoMessage> _handler;

    [Params(1, 5, 10)]
    public int ListenerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _handler = new CompositeIsoMessageHandler<IsoMessage>();
        for (var i = 0; i < ListenerCount; i++)
            _handler.AddListener(new NoOpListener());
    }

    [Benchmark(Description = "AddListener + RemoveListener cycle")]
    public void AddRemoveListener()
    {
        var listener = new NoOpListener();
        _handler.AddListener(listener);
        _handler.RemoveListener(listener);
    }

    private class NoOpListener : IIsoMessageListener<IsoMessage>
    {
        public bool CanHandleMessage(IsoMessage isoMessage) => false;
        public Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage) =>
            Task.FromResult(true);
    }
}
