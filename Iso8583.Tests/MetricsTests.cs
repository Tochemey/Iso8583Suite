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
using Iso8583.Common.Metrics;
using Xunit;

namespace Iso8583.Tests;

public class NullMetricsTests
{
    [Fact]
    public void Instance_IsSingleton()
    {
        Assert.Same(NullIso8583Metrics.Instance, NullIso8583Metrics.Instance);
    }

    [Fact]
    public void AllMethods_DoNotThrow()
    {
        var m = NullIso8583Metrics.Instance;
        m.MessageSent(0x0200);
        m.MessageReceived(0x0200);
        m.MessageHandled(0x0200, TimeSpan.FromMilliseconds(5));
        m.MessageError(0x0200, new Exception("test"));
        m.ConnectionEstablished();
        m.ConnectionLost();
    }
}
