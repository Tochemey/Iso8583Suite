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
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Common.Netty.Pipelines;
using Xunit;

namespace Iso8583.Tests;

public class ConnectionTrackerTests
{
    [Fact]
    public void InitialState_ZeroConnections()
    {
        var tracker = new ConnectionTracker();
        Assert.Equal(0, tracker.ActiveConnectionCount);
        Assert.NotNull(tracker.ActiveChannels);
    }

    [Fact]
    public void IsSharable_ReturnsTrue()
    {
        var tracker = new ConnectionTracker();
        Assert.True(tracker.IsSharable);
    }

    [Fact]
    public void ChannelActive_IncrementsCount()
    {
        var tracker = new ConnectionTracker();
        var channel = new EmbeddedChannel(tracker);

        Assert.Equal(1, tracker.ActiveConnectionCount);
        channel.CloseAsync().Wait();
    }

    [Fact]
    public void ChannelInactive_DecrementsCount()
    {
        var tracker = new ConnectionTracker();
        var channel = new EmbeddedChannel(tracker);

        Assert.Equal(1, tracker.ActiveConnectionCount);
        channel.CloseAsync().Wait();

        // After close, ChannelInactive fires
        Assert.Equal(0, tracker.ActiveConnectionCount);
    }

    [Fact]
    public void MultipleChannels_TracksCorrectly()
    {
        var tracker = new ConnectionTracker();
        var ch1 = new EmbeddedChannel(tracker);
        var ch2 = new EmbeddedChannel(tracker);
        var ch3 = new EmbeddedChannel(tracker);

        Assert.Equal(3, tracker.ActiveConnectionCount);

        ch2.CloseAsync().Wait();
        Assert.Equal(2, tracker.ActiveConnectionCount);

        ch1.CloseAsync().Wait();
        ch3.CloseAsync().Wait();
        Assert.Equal(0, tracker.ActiveConnectionCount);
    }

    [Fact]
    public void MaxConnections_ExceedsLimit_ClosesChannel()
    {
        var tracker = new ConnectionTracker(maxConnections: 2);
        var ch1 = new EmbeddedChannel(tracker);
        var ch2 = new EmbeddedChannel(tracker);

        Assert.Equal(2, tracker.ActiveConnectionCount);

        // Third connection should be rejected
        var ch3 = new EmbeddedChannel(tracker);
        // Count should still be 2 (third was rejected)
        Assert.True(tracker.ActiveConnectionCount <= 2);

        ch1.CloseAsync().Wait();
        ch2.CloseAsync().Wait();
        ch3.CloseAsync().Wait();
    }

    [Fact]
    public void MaxConnections_Zero_MeansUnlimited()
    {
        var tracker = new ConnectionTracker(maxConnections: 0);
        var channels = new EmbeddedChannel[10];
        for (var i = 0; i < 10; i++)
            channels[i] = new EmbeddedChannel(tracker);

        Assert.Equal(10, tracker.ActiveConnectionCount);

        foreach (var ch in channels)
            ch.CloseAsync().Wait();
    }
}
