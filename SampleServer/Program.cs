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
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using Iso8583.Server;
using Microsoft.Extensions.Logging;
using NetCore8583;
using NetCore8583.Parse;
using NLog.Extensions.Logging;
using DotNettyLogLevel = DotNetty.Handlers.Logging.LogLevel;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SampleServer
{
  /// <summary>
  ///   Scenario-driven sample acquirer. Listens on a TCP port and responds to the following
  ///   message flows, which correspond to the scenarios exercised by <c>SampleClient</c>:
  ///
  ///   <list type="bullet">
  ///     <item><description><b>0800 — Network management.</b> Sign-on, sign-off, and echo requests are answered with an 0810 approval (response code 00 / -1 for echo).</description></item>
  ///     <item><description><b>0200 — Purchase authorization.</b> Approves amounts up to $500, declines anything above with response code 61 (exceeds withdrawal amount limit).</description></item>
  ///     <item><description><b>0420 — Reversal advice.</b> Accepts the reversal and returns 0430 with response code 00. Tracks reversed STANs so duplicate reversals are still acknowledged.</description></item>
  ///     <item><description><b>1100 — Legacy authorization.</b> Retained from the original sample so existing demos still work.</description></item>
  ///   </list>
  /// </summary>
  internal class Program
  {
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
      builder.AddNLog());

    private static readonly ILogger _logger = _loggerFactory.CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
      try
      {
        const int isoServerPort = 9000;

        // Build the message factory from the shared n8583.xml definitions.
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        var serverConfig = new ServerConfiguration
        {
          LogSensitiveData = true,
          ReplyOnError = true,
          AddLoggingHandler = true,
          EncodeFrameLengthAsString = true,
          FrameLengthFieldLength = 4,
          // Use the application's configured log level rather than a hardcoded INFO so the
          // DotNetty acceptor / ISO message logging handler honor the NLog configuration.
          LogLevel = DotNettyLogLevel.DEBUG,
          // Enable the structured audit log; events are published to the "Iso8583.Audit"
          // category, which downstream sinks can route to a SIEM or JSON file.
          EnableAuditLog = true,
          AuditLogger = _loggerFactory.CreateLogger(Iso8583AuditLogHandler.AuditLogCategory)
        };

        var serverLogger = _loggerFactory.CreateLogger<Iso8583Server<IsoMessage>>();
        var server = new Iso8583Server<IsoMessage>(isoServerPort, serverConfig, messageFactory, serverLogger);

        // Each listener handles one scenario; CompositeIsoMessageHandler dispatches by CanHandleMessage.
        server.AddMessageListener(new NetworkManagementListener(messageFactory, _logger));
        server.AddMessageListener(new PurchaseAuthorizationListener(messageFactory, _logger));
        server.AddMessageListener(new ReversalListener(messageFactory, _logger));
        server.AddMessageListener(new LegacyAuthorizationListener(messageFactory));

        await server.Start();

        if (server.IsStarted())
        {
          _logger.LogInformation("SampleServer started on port {Port}. Press Ctrl+C to stop.", isoServerPort);

          using var shutdown = new CancellationTokenSource();
          Console.CancelKeyPress += (_, e) =>
          {
            e.Cancel = true;
            shutdown.Cancel();
          };

          try
          {
            await Task.Delay(Timeout.Infinite, shutdown.Token);
          }
          catch (OperationCanceledException)
          {
            // expected on Ctrl+C
          }
        }

        _logger.LogInformation("Shutting down SampleServer...");
        await server.Shutdown();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Server error");
      }
    }

    // ------------------------------------------------------------
    // Scenario: Network Management (0800 → 0810)
    // Handles sign-on, sign-off, and echo. Field 70 carries the
    // network management information code: 001=sign-on, 002=sign-off,
    // 301=echo test.
    // ------------------------------------------------------------
    private class NetworkManagementListener : IIsoMessageListener<IsoMessage>
    {
      private readonly IsoMessageFactory<IsoMessage> _factory;
      private readonly ILogger _logger;

      public NetworkManagementListener(IsoMessageFactory<IsoMessage> factory, ILogger logger)
      {
        _factory = factory;
        _logger = logger;
      }

      public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0800;

      public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
      {
        var nmic = request.HasField(70) ? request.GetField(70).Value?.ToString() : "000";
        var scenario = nmic switch
        {
          "001" => "sign-on",
          "002" => "sign-off",
          "301" => "echo",
          _ => $"nm-{nmic}"
        };
        _logger.LogInformation("[network-management] received {Scenario} (nmic={Nmic})", scenario, nmic);

        var response = _factory.CreateResponse(request);
        response.CopyFieldsFrom(request, 7, 11, 70);
        response.SetField(39, new IsoValue(IsoType.ALPHA, "-1", 2));
        await context.WriteAndFlushAsync(response);
        return false;
      }
    }

    // ------------------------------------------------------------
    // Scenario: Purchase Authorization (0200 → 0210)
    // Demonstrates a simple business rule: approve amounts <= $500,
    // decline larger ones with response code 61.
    // ------------------------------------------------------------
    private class PurchaseAuthorizationListener : IIsoMessageListener<IsoMessage>
    {
      private const long ApprovalLimitMinorUnits = 50000; // $500.00

      private readonly IsoMessageFactory<IsoMessage> _factory;
      private readonly ILogger _logger;

      public PurchaseAuthorizationListener(IsoMessageFactory<IsoMessage> factory, ILogger logger)
      {
        _factory = factory;
        _logger = logger;
      }

      public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0200;

      public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
      {
        var amountRaw = request.HasField(4) ? request.GetField(4).Value?.ToString() : "0";
        _ = long.TryParse(amountRaw, out var amount);

        var stan = request.HasField(11) ? request.GetField(11).Value?.ToString() : "000000";
        var approved = amount <= ApprovalLimitMinorUnits;
        var responseCode = approved ? "00" : "61";

        _logger.LogInformation(
          "[purchase] stan={Stan} amount={Amount} → {Status} ({Code})",
          stan, amount, approved ? "APPROVED" : "DECLINED", responseCode);

        var response = _factory.CreateResponse(request);
        response.CopyFieldsFrom(request, 3, 4, 7, 11, 12, 37, 41, 49);
        response.RemoveFields(13, 14, 19, 22, 24, 26, 35, 45);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "AUTH01", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, responseCode, 2));

        await context.WriteAndFlushAsync(response);
        return false;
      }
    }

    // ------------------------------------------------------------
    // Scenario: Reversal Advice (0420 → 0430)
    // Accepts a reversal for a previously authorized transaction
    // and returns 00 (approved). Idempotent: a repeat reversal for
    // the same STAN is still acknowledged.
    // ------------------------------------------------------------
    private class ReversalListener : IIsoMessageListener<IsoMessage>
    {
      private readonly IsoMessageFactory<IsoMessage> _factory;
      private readonly ILogger _logger;
      private readonly ConcurrentDictionary<string, byte> _reversedStans = new();

      public ReversalListener(IsoMessageFactory<IsoMessage> factory, ILogger logger)
      {
        _factory = factory;
        _logger = logger;
      }

      public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x0420;

      public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
      {
        var stan = request.HasField(11) ? request.GetField(11).Value?.ToString() ?? "000000" : "000000";
        var isDuplicate = !_reversedStans.TryAdd(stan, 0);
        _logger.LogInformation(
          "[reversal] stan={Stan} → {Status}",
          stan, isDuplicate ? "DUPLICATE (still acknowledged)" : "ACCEPTED");

        var response = _factory.CreateResponse(request);
        response.CopyFieldsFrom(request, 3, 4, 7, 11, 37, 41, 49);
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "00", 2));
        await context.WriteAndFlushAsync(response);
        return false;
      }
    }

    // ------------------------------------------------------------
    // Scenario: Legacy 1987 Authorization (1100 → 1110)
    // Preserved from the original sample so existing demos still
    // exercise the 1100 flow.
    // ------------------------------------------------------------
    private class LegacyAuthorizationListener : IIsoMessageListener<IsoMessage>
    {
      private readonly IsoMessageFactory<IsoMessage> _factory;

      public LegacyAuthorizationListener(IsoMessageFactory<IsoMessage> factory) => _factory = factory;

      public bool CanHandleMessage(IsoMessage msg) => msg.Type == 0x1100;

      public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage request)
      {
        var response = _factory.CreateResponse(request);
        var field2 = request.GetField(2);
        var pan = field2?.Value as string;
        response.CopyFieldsFrom(request, 2, 3, 4, 7, 11, 12, 37, 41, 42, 49);
        response.RemoveFields(13, 14, 19, 22, 24, 26, 35, 45);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "123456", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
        if (pan != null && pan.StartsWith("5"))
          response.SetField(15, new IsoValue(IsoType.DATE6, new DateTime()));

        await context.WriteAndFlushAsync(response);
        return false;
      }
    }
  }
}
