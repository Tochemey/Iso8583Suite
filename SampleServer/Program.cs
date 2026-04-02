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
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Server;
using Microsoft.Extensions.Logging;
using NetCore8583;
using NetCore8583.Parse;
using NLog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SampleServer
{
  internal class Program
  {
    private static IsoMessage _receivedMessage;

    private static readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(builder =>
      builder.AddNLog());

    private static readonly ILogger _logger = _loggerFactory.CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
      try
      {
        const int isoServerPort = 9000;
        // create a message factory and load parse/template definitions from n8583.xml
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        // let us create a message factory
        var messageFactory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        // let us configure the server
        var serverConfig = new ServerConfiguration
        {
          LogSensitiveData = true,
          ReplyOnError = true,
          AddLoggingHandler = true,
          EncodeFrameLengthAsString = true,
          FrameLengthFieldLength = 4
        };

        // create the iso server providing port to bind to, ServerConfiguration, and MessageFactory
        var serverLogger = _loggerFactory.CreateLogger<Iso8583Server<IsoMessage>>();
        var server = new Iso8583Server<IsoMessage>(isoServerPort, serverConfig, messageFactory, serverLogger);

        // let us add some custom listener
        server.AddMessageListener(new CustomListener(messageFactory));

        // starts server
        await server.Start();

        // check the server has started
        if (server.IsStarted())
        {
          _logger.LogInformation("server started ready to handle requests");
          // let us wait for request to come
          while (_receivedMessage == null)
            Thread.Sleep(100);
        }

        // stop the server
        await server.Shutdown();
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Server error");
      }
    }

    private class CustomListener : IIsoMessageListener<IsoMessage>
    {
      private readonly IsoMessageFactory<IsoMessage> _messageFactory;

      public CustomListener(IsoMessageFactory<IsoMessage> messageFactory) => _messageFactory = messageFactory;

      public bool CanHandleMessage(IsoMessage isoMessage) => (int)isoMessage.Type == 0x1100;

      public async Task<bool> HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage)
      {
        var response = _messageFactory.CreateResponse(isoMessage);
        var field2 = isoMessage.GetField(2);
        var pan = field2.Value as string;
        response.CopyFieldsFrom(isoMessage, 2, 3, 4, 7, 11, 12, 37, 41, 42, 49);
        response.RemoveFields(13, 14, 19, 22, 24, 26, 35, 45);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "123456", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
        if (pan != null && pan.StartsWith("5")) // MasterCard
          response.SetField(15, new IsoValue(IsoType.DATE6, new DateTime()));

        await context.WriteAndFlushAsync(response);
        // signal that we've handled the message (after response is sent)
        _receivedMessage = isoMessage;
        return false;
      }
    }
  }
}
