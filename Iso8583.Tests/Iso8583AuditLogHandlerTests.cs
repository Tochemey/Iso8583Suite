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
using System.Linq;
using System.Text;
using System.Threading;
using DotNetty.Transport.Channels.Embedded;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using Microsoft.Extensions.Logging;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Tests;

public class Iso8583AuditLogHandlerTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public Iso8583AuditLogHandlerTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    private static IsoMessage BuildPurchase(IsoMessageFactory<IsoMessage> factory, string stan, string pan = "5164123785712481")
    {
        var msg = factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, pan, 16));
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "000000", 6));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000010000", 12));
        msg.SetField(11, new IsoValue(IsoType.NUMERIC, stan, 6));
        msg.SetField(37, new IsoValue(IsoType.ALPHA, "RRN123456789", 12));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "TERM0001", 8));
        return msg;
    }

    [Fact]
    public void Inbound_Message_EmitsSingleAuditEvent_WithCoreProperties()
    {
        var logger = new CapturingLogger();
        var handler = new Iso8583AuditLogHandler(logger);
        var channel = new EmbeddedChannel(handler);

        var request = BuildPurchase(_factory, "000123");
        channel.WriteInbound(request);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Inbound", entry.Scope["Iso8583.Direction"]);
        Assert.Equal("0200", entry.Scope["Iso8583.Mti"]);
        Assert.Equal("000123", entry.Scope["Iso8583.Stan"]);
        Assert.Equal("RRN123456789", entry.Scope["Iso8583.Rrn"]);
        Assert.Equal("02-000123", entry.Scope["Iso8583.CorrelationId"]);
        Assert.False(entry.Scope.ContainsKey("Iso8583.DurationMs"));
        Assert.False(entry.Scope.ContainsKey("Iso8583.Fields"));

        channel.CloseAsync().Wait();
    }

    [Fact]
    public void Outbound_Message_EmitsAuditEvent_WithOutboundDirection()
    {
        var logger = new CapturingLogger();
        var handler = new Iso8583AuditLogHandler(logger);
        var channel = new EmbeddedChannel(handler);

        var msg = BuildPurchase(_factory, "000456");
        channel.WriteOutbound(msg);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Outbound", entry.Scope["Iso8583.Direction"]);
        Assert.Equal("0200", entry.Scope["Iso8583.Mti"]);
        Assert.Equal("000456", entry.Scope["Iso8583.Stan"]);

        channel.CloseAsync().Wait();
    }

    [Fact]
    public void RequestResponse_Correlation_AttachesDurationMs_OnResponse()
    {
        var logger = new CapturingLogger();
        var handler = new Iso8583AuditLogHandler(logger);
        var channel = new EmbeddedChannel(handler);

        // Outbound request
        var request = BuildPurchase(_factory, "000789");
        channel.WriteOutbound(request);

        Thread.Sleep(5); // allow Stopwatch to accumulate measurable elapsed time

        // Inbound response (same STAN, response function → MTI 0x0210)
        var response = _factory.NewMessage(0x0210);
        response.SetField(11, new IsoValue(IsoType.NUMERIC, "000789", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "00", 2));
        channel.WriteInbound(response);

        Assert.Equal(2, logger.Entries.Count);
        var requestEntry = logger.Entries[0];
        var responseEntry = logger.Entries[1];

        Assert.Equal("Outbound", requestEntry.Scope["Iso8583.Direction"]);
        Assert.False(requestEntry.Scope.ContainsKey("Iso8583.DurationMs"));

        Assert.Equal("Inbound", responseEntry.Scope["Iso8583.Direction"]);
        Assert.True(responseEntry.Scope.ContainsKey("Iso8583.DurationMs"));
        var duration = Assert.IsType<double>(responseEntry.Scope["Iso8583.DurationMs"]);
        Assert.True(duration >= 0.0);

        // Correlation id is identical for the request and its response.
        Assert.Equal(requestEntry.Scope["Iso8583.CorrelationId"],
            responseEntry.Scope["Iso8583.CorrelationId"]);

        channel.CloseAsync().Wait();
    }

    [Fact]
    public void IncludeFields_AttachesMaskedFieldMap()
    {
        var logger = new CapturingLogger();
        var handler = new Iso8583AuditLogHandler(logger, includeFields: true);
        var channel = new EmbeddedChannel(handler);

        var msg = BuildPurchase(_factory, "000111", pan: "5164123785712481");
        msg.SetField(35, new IsoValue(IsoType.LLVAR, "5164123785712481D17021011408011015360", 37));

        channel.WriteInbound(msg);

        var entry = Assert.Single(logger.Entries);
        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            entry.Scope["Iso8583.Fields"]);

        // PAN (field 2) is partially masked: first 6 + last 4 visible.
        Assert.Equal("516412******2481", fields["2"]);
        // Track 2 (field 35) is fully masked.
        Assert.Equal("***", fields["35"]);

        channel.CloseAsync().Wait();
    }

    [Fact]
    public void IsSharable_ReturnsFalse_BecauseHandlerIsStateful()
    {
        var handler = new Iso8583AuditLogHandler(new CapturingLogger());
        Assert.False(handler.IsSharable);
    }

    [Fact]
    public void LoggerNotEnabled_EmitsNothing()
    {
        var logger = new CapturingLogger { Enabled = false };
        var handler = new Iso8583AuditLogHandler(logger);
        var channel = new EmbeddedChannel(handler);

        channel.WriteInbound(BuildPurchase(_factory, "000222"));

        Assert.Empty(logger.Entries);

        channel.CloseAsync().Wait();
    }

    // ------------------------------------------------------------
    // Minimal ILogger that captures level, message, and scope state.
    // ------------------------------------------------------------
    private sealed class CapturingLogger : ILogger
    {
        public bool Enabled { get; set; } = true;
        public List<CapturedEntry> Entries { get; } = new();
        private readonly Stack<IReadOnlyDictionary<string, object>> _scopes = new();

        public IDisposable BeginScope<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                var dict = pairs.ToDictionary(p => p.Key, p => p.Value);
                _scopes.Push(dict);
                return new ScopeDisposable(_scopes);
            }
            _scopes.Push(new Dictionary<string, object>());
            return new ScopeDisposable(_scopes);
        }

        public bool IsEnabled(LogLevel logLevel) => Enabled;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!Enabled) return;
            var flat = new Dictionary<string, object>();
            foreach (var scope in _scopes.Reverse())
                foreach (var kv in scope)
                    flat[kv.Key] = kv.Value;
            Entries.Add(new CapturedEntry(logLevel, formatter(state, exception), flat));
        }

        private sealed class ScopeDisposable : IDisposable
        {
            private readonly Stack<IReadOnlyDictionary<string, object>> _scopes;
            public ScopeDisposable(Stack<IReadOnlyDictionary<string, object>> scopes) => _scopes = scopes;
            public void Dispose()
            {
                if (_scopes.Count > 0) _scopes.Pop();
            }
        }
    }

    private sealed record CapturedEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object> Scope);
}
