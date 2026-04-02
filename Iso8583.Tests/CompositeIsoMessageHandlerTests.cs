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
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;
using Xunit;

namespace Iso8583.Tests;

public class CompositeIsoMessageHandlerTests
{
    [Fact]
    public void AddListener_NullListener_ThrowsArgumentNullException()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        Assert.Throws<ArgumentNullException>(() => handler.AddListener(null!));
    }

    [Fact]
    public void AddListeners_NullArray_ThrowsArgumentNullException()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        Assert.Throws<ArgumentNullException>(() => handler.AddListeners(null!));
    }

    [Fact]
    public void AddListeners_EmptyArray_ThrowsArgumentNullException()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        Assert.Throws<ArgumentNullException>(() => handler.AddListeners(Array.Empty<IIsoMessageListener<IsoMessage>>()));
    }

    [Fact]
    public void AddListener_ThenRemove_DoesNotThrow()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        var listener = new TestListener();
        handler.AddListener(listener);
        handler.RemoveListener(listener);
    }

    [Fact]
    public void RemoveListener_NotPresent_DoesNotThrow()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        var listener = new TestListener();
        handler.RemoveListener(listener); // should not throw
    }

    [Fact]
    public async Task AddListener_ConcurrentAccess_DoesNotThrow()
    {
        var handler = new CompositeIsoMessageHandler<IsoMessage>();
        var barrier = new Barrier(10);

        var tasks = new Task[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var j = 0; j < 100; j++)
                {
                    var listener = new TestListener();
                    handler.AddListener(listener);
                    handler.RemoveListener(listener);
                }
            });
        }

        await Task.WhenAll(tasks); // Should complete without exceptions
    }

    private class TestListener : IIsoMessageListener<IsoMessage>
    {
        public bool CanHandleMessage(IsoMessage isoMessage) => true;
        public Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage) =>
            Task.FromResult(false);
    }
}
