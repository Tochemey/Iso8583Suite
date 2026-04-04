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
using Iso8583.Client;

namespace Iso8583.Tests;

public class PooledClientConfigurationTests
{
    [Fact]
    public void Defaults_AreValid()
    {
        var config = new PooledClientConfiguration();
        config.Validate(); // should not throw

        Assert.Equal(4, config.PoolSize);
        Assert.Equal(TimeSpan.FromSeconds(10), config.HealthCheckInterval);
        Assert.NotNull(config.ClientConfiguration);
    }

    [Fact]
    public void Validate_PoolSizeZero_Throws()
    {
        var config = new PooledClientConfiguration { PoolSize = 0 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_NegativePoolSize_Throws()
    {
        var config = new PooledClientConfiguration { PoolSize = -1 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ZeroHealthCheckInterval_Throws()
    {
        var config = new PooledClientConfiguration { HealthCheckInterval = TimeSpan.Zero };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }
}
