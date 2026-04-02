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
}
