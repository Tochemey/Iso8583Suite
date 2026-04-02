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
using Iso8583.Server;
using Xunit;

namespace Iso8583.Tests;

public class ConfigurationValidationTests
{
    [Fact]
    public void ServerConfiguration_DefaultValues_PassValidation()
    {
        var config = new ServerConfiguration();
        config.Validate(); // Should not throw
    }

    [Fact]
    public void ClientConfiguration_DefaultValues_PassValidation()
    {
        var config = new ClientConfiguration();
        config.Validate(); // Should not throw
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_NegativeOrZeroMaxFrameLength_Throws(int maxFrameLength)
    {
        var config = new ServerConfiguration { MaxFrameLength = maxFrameLength };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_NegativeIdleTimeout_Throws()
    {
        var config = new ServerConfiguration { IdleTimeout = -1 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ZeroIdleTimeout_DoesNotThrow()
    {
        var config = new ServerConfiguration { IdleTimeout = 0 };
        config.Validate(); // Should not throw (0 = disabled)
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidWorkerThreadCount_Throws(int count)
    {
        var config = new ServerConfiguration { WorkerThreadCount = count };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_InvalidFrameLengthFieldLength_Throws(int length)
    {
        var config = new ServerConfiguration { FrameLengthFieldLength = length };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Validate_ValidFrameLengthFieldLength_DoesNotThrow(int length)
    {
        var config = new ServerConfiguration { FrameLengthFieldLength = length };
        config.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_NegativeFrameLengthFieldOffset_Throws()
    {
        var config = new ServerConfiguration { FrameLengthFieldOffset = -1 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ClientValidate_InvalidReconnectInterval_Throws(int interval)
    {
        var config = new ClientConfiguration { ReconnectInterval = interval };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void ClientValidate_NegativeMaxReconnectDelay_Throws()
    {
        var config = new ClientConfiguration { MaxReconnectDelay = -1 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void ClientValidate_NegativeMaxReconnectAttempts_Throws()
    {
        var config = new ClientConfiguration { MaxReconnectAttempts = -1 };
        Assert.Throws<ArgumentException>(() => config.Validate());
    }

    [Fact]
    public void ClientValidate_ZeroMaxReconnectAttempts_DoesNotThrow()
    {
        var config = new ClientConfiguration { MaxReconnectAttempts = 0 };
        config.Validate(); // Should not throw (0 = unlimited)
    }
}
