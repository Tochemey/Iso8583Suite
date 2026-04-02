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
  internal class Program
  {
    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
      builder.AddNLog());

    private static readonly ILogger _logger = _loggerFactory.CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
      const int serverPort = 9000;
      const string serverHost = "127.0.0.1";

      // create a message factory and load parse/template definitions from n8583.xml
      var mfact = ConfigParser.CreateDefault();
      ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
      mfact.UseBinaryMessages = false;
      mfact.Encoding = Encoding.ASCII;

      // let us create a message factory
      var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

      // let us configure the client
      var clientConfig = new ClientConfiguration
      {
        LogSensitiveData = true,
        ReplyOnError = true,
        IdleTimeout = 5,
        AddLoggingHandler = true,
        EncodeFrameLengthAsString = true,
        FrameLengthFieldLength = 4
      };

      // configure the iso client
      var client = new Iso8583Client<IsoMessage>(clientConfig, messageFactory);

      // Establish a connection.
      await client.Connect(serverHost, serverPort);

      // check whether the connection is established
      if (!client.IsConnected())
      {
        _logger.LogInformation("client is not connected. Shutting down");
        Environment.Exit(1);
      }

      _logger.LogInformation("client is connected");
      const string pan = "5164123785712481";
      const string track2 = pan + "D17021011408011015360";
      const string track1 = "B" + pan + "^SUPPLIED/NOT^17021011408011015360";

      var message = messageFactory.NewMessage(0x1100);
      message.SetField(2, new IsoValue(IsoType.LLVAR, pan, pan.Length));
      message.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
      message.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
      message.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
      message.SetField(12, new IsoValue(IsoType.DATE12, DateTime.UtcNow));
      message.SetField(14, new IsoValue(IsoType.DATE_EXP, new DateTime(2017, 2, 1)));
      message.SetField(19, new IsoValue(IsoType.NUMERIC, "840", 3));
      message.SetField(22, new IsoValue(IsoType.ALPHA, "A00101A03346", 12));
      message.SetField(24, new IsoValue(IsoType.NUMERIC, "100", 3));
      message.SetField(26, new IsoValue(IsoType.NUMERIC, "5814", 4));
      message.SetField(35, new IsoValue(IsoType.LLVAR, track2, track2.Length));
      message.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
      message.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
      message.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
      message.SetField(45, new IsoValue(IsoType.LLVAR, track1, track1.Length));
      message.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));

      // send the iso message and wait for correlated response
      _logger.LogDebug("sending message {Type} to the server", message.Type.ToString("X4"));
      var response = await client.SendAndReceive(message, TimeSpan.FromSeconds(10));
      _logger.LogInformation("received response type 0x{Type}", response.Type.ToString("X4"));

      // disconnect
      await client.Disconnect();
    }

  }
}
