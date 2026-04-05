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
using Iso8583.Client;
using Iso8583.Common.Iso;
using Microsoft.Extensions.Logging;
using NetCore8583;
using NetCore8583.Parse;
using NLog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SampleClient
{
  /// <summary>
  ///   Scenario-driven sample issuer. Connects to the SampleServer and walks through a
  ///   realistic end-to-end session:
  ///
  ///   <list type="number">
  ///     <item><description>Sign-on (0800, nmic=001).</description></item>
  ///     <item><description>Echo test (0800, nmic=301).</description></item>
  ///     <item><description>Purchase approved (0200, amount below the approval limit).</description></item>
  ///     <item><description>Purchase declined (0200, amount above the approval limit).</description></item>
  ///     <item><description>Reversal of the declined purchase (0420).</description></item>
  ///     <item><description>Sign-off (0800, nmic=002).</description></item>
  ///   </list>
  ///
  ///   Each scenario is implemented as a single async method below so the code reads top-to-bottom
  ///   as a reference for building your own ISO 8583 client integrations.
  /// </summary>
  internal class Program
  {
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
      builder.AddNLog());

    private static readonly ILogger _logger = _loggerFactory.CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
      const int serverPort = 9000;
      const string serverHost = "127.0.0.1";

      var mfact = ConfigParser.CreateDefault();
      ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
      mfact.UseBinaryMessages = false;
      mfact.Encoding = Encoding.ASCII;

      var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

      var clientConfig = new ClientConfiguration
      {
        LogSensitiveData = true,
        ReplyOnError = true,
        IdleTimeout = 30,
        AddLoggingHandler = true,
        EncodeFrameLengthAsString = true,
        FrameLengthFieldLength = 4
      };

      await using var client = new Iso8583Client<IsoMessage>(clientConfig, messageFactory);
      await client.Connect(serverHost, serverPort);

      if (!client.IsConnected())
      {
        _logger.LogError("client failed to connect to {Host}:{Port}", serverHost, serverPort);
        Environment.Exit(1);
      }

      _logger.LogInformation("client connected to {Host}:{Port}", serverHost, serverPort);

      var session = new SessionContext();

      await SignOn(client, messageFactory, session);
      await EchoTest(client, messageFactory, session);
      await PurchaseApproved(client, messageFactory, session);
      await PurchaseDeclined(client, messageFactory, session);
      await ReverseLastTransaction(client, messageFactory, session);
      await SignOff(client, messageFactory, session);

      _logger.LogInformation("all scenarios completed, disconnecting");
      await client.Disconnect();
    }

    // ------------------------------------------------------------
    // Scenario 1: Sign-on (0800 with nmic=001)
    // ------------------------------------------------------------
    private static async Task SignOn(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: SIGN-ON ---");

      var request = factory.NewMessage(0x0800);
      request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
      request.SetField(11, new IsoValue(IsoType.NUMERIC, session.NextStan(), 6));
      request.SetField(70, new IsoValue(IsoType.NUMERIC, "001", 3));

      var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));
      _logger.LogInformation("sign-on response type=0x{Type:X4} code={Code}",
        response.Type, response.GetField(39)?.Value);
    }

    // ------------------------------------------------------------
    // Scenario 2: Echo test (0800 with nmic=301)
    // ------------------------------------------------------------
    private static async Task EchoTest(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: ECHO ---");

      var request = factory.NewMessage(0x0800);
      request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
      request.SetField(11, new IsoValue(IsoType.NUMERIC, session.NextStan(), 6));
      request.SetField(70, new IsoValue(IsoType.NUMERIC, "301", 3));

      var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));
      _logger.LogInformation("echo response type=0x{Type:X4}", response.Type);
    }

    // ------------------------------------------------------------
    // Scenario 3: Purchase approved (0200, amount below limit)
    // ------------------------------------------------------------
    private static async Task PurchaseApproved(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: PURCHASE (approved) ---");

      var request = BuildPurchaseRequest(factory, session, amountMinorUnits: 12345); // $123.45
      var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));
      var code = response.GetField(39)?.Value?.ToString() ?? "??";
      _logger.LogInformation("purchase response code={Code} (expected 00)", code);
    }

    // ------------------------------------------------------------
    // Scenario 4: Purchase declined (0200, amount over limit)
    // The server responds with 61. We remember this transaction
    // so the next scenario can reverse it.
    // ------------------------------------------------------------
    private static async Task PurchaseDeclined(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: PURCHASE (declined) ---");

      var request = BuildPurchaseRequest(factory, session, amountMinorUnits: 99999); // $999.99
      session.LastPurchaseRequest = request;
      var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));
      var code = response.GetField(39)?.Value?.ToString() ?? "??";
      _logger.LogInformation("purchase response code={Code} (expected 61)", code);
    }

    // ------------------------------------------------------------
    // Scenario 5: Reversal (0420)
    // Copies the key fields from the previous authorization request
    // into a reversal advice. Realistic flow for a timed-out or
    // rejected-by-host transaction.
    // ------------------------------------------------------------
    private static async Task ReverseLastTransaction(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: REVERSAL ---");

      var original = session.LastPurchaseRequest
                     ?? throw new InvalidOperationException("No previous purchase to reverse");

      var reversal = factory.NewMessage(0x0420);
      reversal.SetField(3, original.GetField(3));
      reversal.SetField(4, original.GetField(4));
      reversal.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
      reversal.SetField(11, new IsoValue(IsoType.NUMERIC, session.NextStan(), 6));
      if (original.HasField(37)) reversal.SetField(37, original.GetField(37));
      if (original.HasField(41)) reversal.SetField(41, original.GetField(41));
      if (original.HasField(49)) reversal.SetField(49, original.GetField(49));

      var response = await client.SendAndReceive(reversal, TimeSpan.FromSeconds(5));
      var code = response.GetField(39)?.Value?.ToString() ?? "??";
      _logger.LogInformation("reversal response code={Code} (expected 00)", code);
    }

    // ------------------------------------------------------------
    // Scenario 6: Sign-off (0800 with nmic=002)
    // ------------------------------------------------------------
    private static async Task SignOff(Iso8583Client<IsoMessage> client,
      IsoMessageFactory<IsoMessage> factory, SessionContext session)
    {
      _logger.LogInformation("--- scenario: SIGN-OFF ---");

      var request = factory.NewMessage(0x0800);
      request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
      request.SetField(11, new IsoValue(IsoType.NUMERIC, session.NextStan(), 6));
      request.SetField(70, new IsoValue(IsoType.NUMERIC, "002", 3));

      var response = await client.SendAndReceive(request, TimeSpan.FromSeconds(5));
      _logger.LogInformation("sign-off response type=0x{Type:X4}", response.Type);
    }

    // Builds a 0200 purchase request used by both the approved and declined scenarios.
    private static IsoMessage BuildPurchaseRequest(IsoMessageFactory<IsoMessage> factory,
      SessionContext session, long amountMinorUnits)
    {
      var request = factory.NewMessage(0x0200);
      request.SetField(3, new IsoValue(IsoType.NUMERIC, "000000", 6));
      request.SetField(4, new IsoValue(IsoType.AMOUNT, amountMinorUnits));
      request.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
      request.SetField(11, new IsoValue(IsoType.NUMERIC, session.NextStan(), 6));
      request.SetField(12, new IsoValue(IsoType.TIME, DateTime.UtcNow));
      request.SetField(13, new IsoValue(IsoType.DATE4, DateTime.UtcNow));
      request.SetField(37, new IsoValue(IsoType.NUMERIC, session.NextRrn(), 12));
      request.SetField(41, new IsoValue(IsoType.ALPHA, "TRM00001", 16));
      request.SetField(49, new IsoValue(IsoType.ALPHA, "840", 3));
      return request;
    }

    // ------------------------------------------------------------
    // Session bookkeeping: monotonic STAN / RRN and the last
    // authorization request (so the reversal scenario can read it).
    // ------------------------------------------------------------
    private class SessionContext
    {
      private int _stan;
      private long _rrn;

      public IsoMessage LastPurchaseRequest { get; set; }

      public string NextStan() => (++_stan).ToString("D6");
      public string NextRrn() => (++_rrn + 100000000000L).ToString("D12");
    }
  }
}
