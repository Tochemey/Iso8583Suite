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

using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Common.Netty.Pipelines;
using Xunit;

namespace Iso8583.Tests;

public class ReconnectOnCloseHandlerTests
{
    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var handler = new ReconnectOnCloseHandler(() => Task.CompletedTask, 100);
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_NullReconnectFunc_ThrowsArgumentNullException()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            new ReconnectOnCloseHandler(null!, 100));
    }

    [Fact]
    public void ResetAttempts_DoesNotThrow()
    {
        var handler = new ReconnectOnCloseHandler(() => Task.CompletedTask, 100);
        handler.ResetAttempts();
    }

    [Fact]
    public void Constructor_CustomParameters_DoesNotThrow()
    {
        var handler = new ReconnectOnCloseHandler(
            () => Task.CompletedTask,
            baseDelay: 200,
            maxDelay: 60000,
            maxAttempts: 20);
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_ZeroMaxAttempts_MeansUnlimited()
    {
        // Should not throw - 0 means unlimited
        var handler = new ReconnectOnCloseHandler(
            () => Task.CompletedTask,
            baseDelay: 100,
            maxAttempts: 0);
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task ReconnectFunc_Throws_SelfRescheduleLoopKeepsRunning()
    {
        // When the reconnect delegate throws, RunReconnectAsync should log the failure
        // and self-schedule the next attempt so the retry loop survives. We drive one
        // ChannelInactive to kick off the first attempt and then wait until the delegate
        // has been invoked at least twice, proving the self-reschedule path executed.
        // maxAttempts is 2 so the loop terminates in a bounded amount of time even on a
        // slow CI thread pool; the important assertion is that invocation 2 happened
        // without any external ChannelInactive trigger.
        var invocations = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = 0;
        var handler = new ReconnectOnCloseHandler(
            () =>
            {
                var n = Interlocked.Increment(ref counter);
                if (n >= 2) invocations.TrySetResult(true);
                throw new System.InvalidOperationException("simulated reconnect failure");
            },
            baseDelay: 20,
            maxDelay: 40,
            maxAttempts: 2);

        var channel = new EmbeddedChannel(handler);
        await channel.CloseAsync();

        // A 10-second budget is far larger than the expected wall time (≈50ms) but is
        // generous enough to absorb thread-pool starvation on a loaded CI worker.
        var completed = await Task.WhenAny(invocations.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(completed == invocations.Task,
            $"self-reschedule loop did not reach 2 invocations in time; counter={Volatile.Read(ref counter)}");
    }

    [Fact]
    public async Task Stop_PreventsFurtherAttempts()
    {
        var invocations = 0;
        var handler = new ReconnectOnCloseHandler(
            () => { Interlocked.Increment(ref invocations); return Task.CompletedTask; },
            baseDelay: 10,
            maxAttempts: 5);

        handler.Stop();

        var channel = new EmbeddedChannel(handler);
        await channel.CloseAsync();
        await Task.Delay(100);

        Assert.Equal(0, invocations);
        Assert.Equal(0, handler.CurrentAttempts);
    }

    [Fact]
    public async Task MaxAttemptsReached_LogsAndStops()
    {
        var handler = new ReconnectOnCloseHandler(
            () => Task.CompletedTask,
            baseDelay: 100_000,
            maxDelay: 100_000,
            maxAttempts: 1);

        // First ChannelInactive bumps _attempt to 1.
        var c1 = new EmbeddedChannel(handler);
        await c1.CloseAsync();
        // Second ChannelInactive enters ScheduleReconnect with _attempt==1, hitting the
        // "max reconnection attempts reached" branch which logs and returns.
        var c2 = new EmbeddedChannel(handler);
        await c2.CloseAsync();

        Assert.True(handler.HasExhaustedAttempts);
    }
}
