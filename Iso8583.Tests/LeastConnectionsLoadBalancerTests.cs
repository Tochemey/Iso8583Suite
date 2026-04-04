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
using System.Collections.Generic;
using Iso8583.Client;

namespace Iso8583.Tests;

public class LeastConnectionsLoadBalancerTests
{
    [Fact]
    public void Select_ReturnsConnectionWithFewestPending()
    {
        var pendingCounts = new Dictionary<int, int>
        {
            { 0, 5 },
            { 1, 2 },
            { 2, 8 }
        };

        var balancer = new LeastConnectionsLoadBalancer(index => pendingCounts[index]);
        ReadOnlySpan<int> active = [0, 1, 2];

        Assert.Equal(1, balancer.Select(active));
    }

    [Fact]
    public void Select_WithTiedCounts_ReturnsFirst()
    {
        var pendingCounts = new Dictionary<int, int>
        {
            { 0, 3 },
            { 1, 3 },
            { 2, 3 }
        };

        var balancer = new LeastConnectionsLoadBalancer(index => pendingCounts[index]);
        ReadOnlySpan<int> active = [0, 1, 2];

        Assert.Equal(0, balancer.Select(active));
    }

    [Fact]
    public void Select_WithSingleConnection_ReturnsThatConnection()
    {
        var balancer = new LeastConnectionsLoadBalancer(_ => 10);
        ReadOnlySpan<int> active = [2];

        Assert.Equal(2, balancer.Select(active));
    }

    [Fact]
    public void Select_WithNoActiveConnections_Throws()
    {
        var balancer = new LeastConnectionsLoadBalancer(_ => 0);
        Assert.Throws<InvalidOperationException>(() => balancer.Select(ReadOnlySpan<int>.Empty));
    }

    [Fact]
    public void Constructor_WithNullProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LeastConnectionsLoadBalancer(null!));
    }

    [Fact]
    public void Select_SkipsInactiveConnections()
    {
        var pendingCounts = new Dictionary<int, int>
        {
            { 0, 10 }, // active, high load
            { 1, 1 },  // inactive (not in active list)
            { 2, 5 },  // active, medium load
            { 3, 2 }   // active, low load
        };

        var balancer = new LeastConnectionsLoadBalancer(index => pendingCounts[index]);
        // Connection 1 is not in the active list even though it has lowest pending
        ReadOnlySpan<int> active = [0, 2, 3];

        Assert.Equal(3, balancer.Select(active));
    }
}
