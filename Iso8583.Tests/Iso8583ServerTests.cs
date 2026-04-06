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
using System.Threading.Tasks;
using Iso8583.Common.Iso;
using Iso8583.Common.Metrics;
using Iso8583.Server;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

[Collection(nameof(TcpServerCollection))]
public class Iso8583ServerTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public Iso8583ServerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    [Fact(Timeout = 30_000)]
    public async Task Start_And_Shutdown_Succeeds()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.Start();
        Assert.True(server.IsStarted());
        await server.Shutdown(TimeSpan.FromSeconds(1));
    }

    [Fact(Timeout = 30_000)]
    public async Task Shutdown_WithGracePeriod_Completes()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.Start();
        await server.Shutdown(TimeSpan.FromMilliseconds(100));
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeAsync_WhenStarted_ShutsDown()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.Start();
        Assert.True(server.IsStarted());
        await server.DisposeAsync();
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeAsync_WhenNotStarted_DoesNotThrow()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.DisposeAsync();
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeAsync_MultipleCalls_Safe()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.Start();
        await server.DisposeAsync();
        await server.DisposeAsync(); // second call safe
    }

    [Fact(Timeout = 30_000)]
    public async Task Start_AfterDispose_ThrowsObjectDisposed()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await server.Start());
    }

    [Fact(Timeout = 30_000)]
    public async Task ActiveConnectionCount_NoClients_IsZero()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), new ServerConfiguration(), _factory);
        await server.Start();
        Assert.Equal(0, server.ActiveConnectionCount);
        Assert.NotNull(server.ActiveConnections);
        await server.Shutdown(TimeSpan.FromSeconds(1));
    }

    [Fact(Timeout = 30_000)]
    public async Task Constructor_WithMetrics_DoesNotThrow()
    {
        var server = new Iso8583Server<IsoMessage>(
            TestPorts.Next(), new ServerConfiguration(), _factory,
            metrics: NullIso8583Metrics.Instance);
        await server.Start();
        await server.Shutdown(TimeSpan.FromSeconds(1));
    }

    [Fact(Timeout = 30_000)]
    public async Task Constructor_MinimalParams_UsesDefaults()
    {
        var server = new Iso8583Server<IsoMessage>(TestPorts.Next(), _factory);
        await server.Start();
        Assert.True(server.IsStarted());
        await server.Shutdown(TimeSpan.FromSeconds(1));
    }
}
