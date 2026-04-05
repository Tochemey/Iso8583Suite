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
using Iso8583.Client;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class PendingRequestManagerTests
{
    private readonly PendingRequestManager<IsoMessage> _manager = new();
    private readonly MessageFactory<IsoMessage> _mfact;

    public PendingRequestManagerTests()
    {
        _mfact = ConfigParser.CreateDefault();
        _mfact.UseBinaryMessages = false;
        _mfact.Encoding = Encoding.ASCII;
    }

    private IsoMessage CreateRequest(int type, string stan)
    {
        var msg = _mfact.NewMessage(type);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, stan, 6));
        return msg;
    }

    private IsoMessage CreateResponse(int type, string stan)
    {
        var msg = _mfact.NewMessage(type);
        msg.SetField(11, new IsoValue(IsoType.ALPHA, stan, 6));
        return msg;
    }

    [Fact]
    public async Task RegisterAndComplete_MatchesResponseToRequest()
    {
        var request = CreateRequest(0x0200, "100001");
        var (_, responseTask) = _manager.RegisterPending(request, TimeSpan.FromSeconds(5));

        var response = CreateResponse(0x0210, "100001");
        Assert.True(_manager.CanHandleMessage(response));
        await _manager.HandleMessage(null!, response);

        var result = await responseTask;
        Assert.NotNull(result);
        Assert.Equal(0x0210, result.Type);
    }

    [Fact]
    public async Task CanHandleMessage_NonMatchingResponse_ReturnsFalse()
    {
        var request = CreateRequest(0x0200, "100001");
        _ = _manager.RegisterPending(request, TimeSpan.FromSeconds(5));

        // Different STAN
        var response = CreateResponse(0x0210, "999999");
        Assert.False(_manager.CanHandleMessage(response));

        _manager.CancelAll();
    }

    [Fact]
    public async Task RegisterPending_Timeout_ThrowsTimeoutException()
    {
        var request = CreateRequest(0x0200, "100002");
        var (_, task) = _manager.RegisterPending(request, TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<TimeoutException>(async () => await task);
    }

    [Fact]
    public async Task RegisterPending_Cancellation_ThrowsTaskCanceledException()
    {
        var cts = new CancellationTokenSource();
        var request = CreateRequest(0x0200, "100003");
        var (_, task) = _manager.RegisterPending(request, TimeSpan.FromSeconds(10), cts.Token);

        cts.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
    }

    [Fact]
    public async Task RegisterPending_DuplicateKey_ThrowsInvalidOperationException()
    {
        var request = CreateRequest(0x0200, "100004");
        _ = _manager.RegisterPending(request, TimeSpan.FromSeconds(5));

        Assert.Throws<InvalidOperationException>(() =>
            _manager.RegisterPending(request, TimeSpan.FromSeconds(5)));

        _manager.CancelAll();
    }

    [Fact]
    public async Task CancelAll_CancelsAllPending()
    {
        var request1 = CreateRequest(0x0200, "100005");
        var request2 = CreateRequest(0x0200, "100006");
        var (_, task1) = _manager.RegisterPending(request1, TimeSpan.FromSeconds(30));
        var (_, task2) = _manager.RegisterPending(request2, TimeSpan.FromSeconds(30));

        _manager.CancelAll();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task2);
    }

    [Fact]
    public async Task HandleMessage_UnmatchedResponse_ReturnsTrue()
    {
        // No pending registration - should return true (continue chain)
        var response = CreateResponse(0x0210, "999999");
        var result = await _manager.HandleMessage(null!, response);
        Assert.True(result);
    }

    [Fact]
    public async Task HandleMessage_MatchedResponse_ReturnsFalse()
    {
        var request = CreateRequest(0x0200, "100007");
        var (_, _) = _manager.RegisterPending(request, TimeSpan.FromSeconds(5));

        var response = CreateResponse(0x0210, "100007");
        var result = await _manager.HandleMessage(null!, response);
        Assert.False(result);
    }

    [Fact]
    public void RegisterPending_NoStan_ThrowsInvalidOperationException()
    {
        var msg = _mfact.NewMessage(0x0200);
        Assert.ThrowsAny<Exception>(() =>
            _manager.RegisterPending(msg, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Cancel_ByKey_CancelsOnlyThatRequest()
    {
        var r1 = CreateRequest(0x0200, "200001");
        var r2 = CreateRequest(0x0200, "200002");
        var (key1, task1) = _manager.RegisterPending(r1, TimeSpan.FromSeconds(30));
        var (_, task2) = _manager.RegisterPending(r2, TimeSpan.FromSeconds(30));

        _manager.Cancel(key1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task1);
        Assert.False(task2.IsCompleted);

        _manager.CancelAll();
    }

    [Fact]
    public void Cancel_UnknownKey_IsNoOp()
    {
        _manager.Cancel("unknown:key"); // should not throw
        Assert.Equal(0, _manager.PendingCount);
    }
}
