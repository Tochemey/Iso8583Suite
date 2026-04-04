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

public class RoundRobinLoadBalancerTests
{
    [Fact]
    public void Select_CyclesThroughActiveConnections()
    {
        var balancer = new RoundRobinLoadBalancer();
        ReadOnlySpan<int> active = [0, 1, 2];

        var first = balancer.Select(active);
        var second = balancer.Select(active);
        var third = balancer.Select(active);
        var fourth = balancer.Select(active);

        // Should cycle: the 4th selection wraps around to the same as the 1st
        Assert.Equal(first, fourth);
        // All three indices should be hit
        var selected = new HashSet<int> { first, second, third };
        Assert.Equal(3, selected.Count);
    }

    [Fact]
    public void Select_WithSingleConnection_AlwaysReturnsSame()
    {
        var balancer = new RoundRobinLoadBalancer();
        ReadOnlySpan<int> active = [2];

        for (var i = 0; i < 10; i++)
            Assert.Equal(2, balancer.Select(active));
    }

    [Fact]
    public void Select_WithNoActiveConnections_Throws()
    {
        var balancer = new RoundRobinLoadBalancer();
        Assert.Throws<InvalidOperationException>(() => balancer.Select(ReadOnlySpan<int>.Empty));
    }

    [Fact]
    public void Select_SkipsInactiveConnections()
    {
        var balancer = new RoundRobinLoadBalancer();
        // Only connections 0 and 3 are active out of 4 total
        ReadOnlySpan<int> active = [0, 3];

        var results = new HashSet<int>();
        for (var i = 0; i < 10; i++)
            results.Add(balancer.Select(active));

        // Should only ever select from the active set
        Assert.Equal(2, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(3, results);
    }
}
