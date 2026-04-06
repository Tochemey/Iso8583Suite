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
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Client;
using Iso8583.Client.HealthChecks;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using Iso8583.Server;
using Iso8583.Server.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

[Collection(nameof(TcpServerCollection))]
public class HealthCheckTests
{
    private static IsoMessageFactory<IsoMessage> CreateFactory()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        return new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    private static ServerConfiguration ServerCfg() => new()
    {
        EncodeFrameLengthAsString = true,
        FrameLengthFieldLength = 4,
        IdleTimeout = 60
    };

    private static ClientConfiguration ClientCfg(bool autoReconnect = false) => new()
    {
        EncodeFrameLengthAsString = true,
        FrameLengthFieldLength = 4,
        IdleTimeout = 60,
        AutoReconnect = autoReconnect
    };

    // ---------------- Client health check ----------------

    [Fact(Timeout = 30_000)]
    public async Task ClientHealthCheck_BeforeConnect_ReportsUnhealthy()
    {
        var factory = CreateFactory();
        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(), factory);
        var check = new Iso8583ClientHealthCheck<IsoMessage>(client);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False((bool)result.Data["connected"]);
        Assert.False((bool)result.Data["reconnecting"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task ClientHealthCheck_WhenConnected_ReportsHealthy()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(), factory);
        await client.Connect("127.0.0.1", port);

        var check = new Iso8583ClientHealthCheck<IsoMessage>(client);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True((bool)result.Data["connected"]);

        await client.Disconnect();
    }

    [Fact(Timeout = 30_000)]
    public async Task ClientHealthCheck_AfterExplicitDisconnect_ReportsUnhealthy()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        var client = new Iso8583Client<IsoMessage>(ClientCfg(autoReconnect: true), factory);
        await client.Connect("127.0.0.1", port);
        await client.Disconnect();

        var check = new Iso8583ClientHealthCheck<IsoMessage>(client);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // An intentional disconnect must not be treated as "reconnecting".
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False((bool)result.Data["reconnecting"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task ClientHealthCheck_DuringReconnect_ReportsDegraded()
    {
        var factory = CreateFactory();

        // Build a ReconnectOnCloseHandler and drive ChannelInactive through an
        // EmbeddedChannel so that CurrentAttempts is bumped to 1. Then inject it
        // into a client that was never connected — this puts the client into the
        // exact IsReconnecting=true state without requiring a flaky server drop.
        var handler = new ReconnectOnCloseHandler(
            () => Task.CompletedTask,
            baseDelay: 100_000, // large delay so the scheduled retry never fires during the test
            maxDelay: 100_000,
            maxAttempts: 5);

        var embedded = new EmbeddedChannel(handler);
        await embedded.CloseAsync(); // triggers ChannelInactive → _attempt++
        Assert.Equal(1, handler.CurrentAttempts);

        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(autoReconnect: true), factory);
        client._reconnectHandler = handler;

        Assert.False(client.IsConnected());
        Assert.True(client.IsReconnecting);

        var check = new Iso8583ClientHealthCheck<IsoMessage>(client);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.True((bool)result.Data["reconnecting"]);
        Assert.False((bool)result.Data["connected"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task ClientHealthCheck_AfterMaxAttemptsExhausted_ReportsUnhealthy()
    {
        var factory = CreateFactory();

        // maxAttempts: 1 → the first ChannelInactive bumps _attempt to 1, the second call
        // hits the "Max reconnection attempts reached" branch and the handler stops retrying.
        var handler = new ReconnectOnCloseHandler(
            () => Task.CompletedTask,
            baseDelay: 100_000,
            maxDelay: 100_000,
            maxAttempts: 1);

        var embedded1 = new EmbeddedChannel(handler);
        await embedded1.CloseAsync(); // attempt 1
        var embedded2 = new EmbeddedChannel(handler);
        await embedded2.CloseAsync(); // hits the exhaustion branch; _attempt stays at 1
        Assert.True(handler.HasExhaustedAttempts);

        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(autoReconnect: true), factory);
        client._reconnectHandler = handler;

        Assert.False(client.IsReconnecting);

        var check = new Iso8583ClientHealthCheck<IsoMessage>(client);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False((bool)result.Data["reconnecting"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task AddIso8583ClientHealthCheck_FromDI_RegistersCheck()
    {
        var factory = CreateFactory();
        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(), factory);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        services.AddHealthChecks().AddIso8583ClientHealthCheck<IsoMessage>("client-check");

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync();

        Assert.Contains("client-check", report.Entries.Keys);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["client-check"].Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task AddIso8583ClientHealthCheck_ExplicitInstance_RegistersCheck()
    {
        var factory = CreateFactory();
        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(), factory);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddIso8583ClientHealthCheck(client, "explicit-client");

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<HealthCheckService>();
        var report = await service.CheckHealthAsync();

        Assert.Contains("explicit-client", report.Entries.Keys);
    }

    // ---------------- Server health check ----------------

    [Fact(Timeout = 30_000)]
    public async Task ServerHealthCheck_BeforeStart_ReportsUnhealthy()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);

        var check = new Iso8583ServerHealthCheck<IsoMessage>(server);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.False((bool)result.Data["listening"]);
        Assert.Equal(0, (int)result.Data["activeConnections"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task ServerHealthCheck_WhenListening_ReportsHealthyWithZeroConnections()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        var check = new Iso8583ServerHealthCheck<IsoMessage>(server);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True((bool)result.Data["listening"]);
        Assert.Equal(0, (int)result.Data["activeConnections"]);
    }

    [Fact(Timeout = 30_000)]
    public async Task ServerHealthCheck_WithActiveClient_ReportsConnectionCount()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        await using var client = new Iso8583Client<IsoMessage>(ClientCfg(), factory);
        await client.Connect("127.0.0.1", port);

        // Give the server a moment to register the inbound connection.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (server.ActiveConnectionCount == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        var check = new Iso8583ServerHealthCheck<IsoMessage>(server);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True((int)result.Data["activeConnections"] >= 1);

        await client.Disconnect();
    }

    [Fact(Timeout = 30_000)]
    public async Task ServerHealthCheck_AfterShutdown_ReportsUnhealthy()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);
        await server.Shutdown(TimeSpan.FromMilliseconds(100));

        var check = new Iso8583ServerHealthCheck<IsoMessage>(server);
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task AddIso8583ServerHealthCheck_FromDI_RegistersCheck()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(server);
        services.AddHealthChecks().AddIso8583ServerHealthCheck<IsoMessage>("server-check");

        var provider = services.BuildServiceProvider();
        var hc = provider.GetRequiredService<HealthCheckService>();
        var report = await hc.CheckHealthAsync();

        Assert.Contains("server-check", report.Entries.Keys);
        Assert.Equal(HealthStatus.Healthy, report.Entries["server-check"].Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task AddIso8583ServerHealthCheck_ExplicitInstance_RegistersCheck()
    {
        var port = TestPorts.Next();
        var factory = CreateFactory();
        await using var server = new Iso8583Server<IsoMessage>(port, ServerCfg(), factory);
        await server.Start();
        await Task.Delay(200);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddIso8583ServerHealthCheck(server, "explicit-server");

        var provider = services.BuildServiceProvider();
        var hc = provider.GetRequiredService<HealthCheckService>();
        var report = await hc.CheckHealthAsync();

        Assert.Contains("explicit-server", report.Entries.Keys);
        Assert.Equal(HealthStatus.Healthy, report.Entries["explicit-server"].Status);
    }
}
