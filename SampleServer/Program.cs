using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Server;
using NetCore8583;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SampleServer
{
  internal class Program
  {
    private static IsoMessage _receivedMessage;

    private static readonly Logger _logger = new LoggerConfiguration().MinimumLevel.Debug()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
      .Enrich.FromLogContext()
      .WriteTo.Console().CreateLogger();

    private static async Task Main(string[] args)
    {
      try
      {
        const int isoServerPort = 9000;
        // let us create a message factory
        var messageFactory = new IsoMessageFactory<IsoMessage>(Iso8583Version.V1987);

        // let us configure the server
        var serverConfig = new ServerConfiguration
        {
          LogSensitiveData = true,
          ReplyOnError = true,
          AddLoggingHandler = true
        };

        // let us create the iso server providing port to bind to, ServerConfiguration, and MessageFactory
        var server = new Iso8583Server<IsoMessage>(isoServerPort, serverConfig, messageFactory, _logger);

        // let us add some custom listener
        server.AddMessageListener(new CustomListener(messageFactory));

        // starts server 
        await server.Start();

        // check the server has started
        if (server.IsStarted())
          _logger.Information("server started ready to handle requests");
          // let us wait for request to come
          while (_receivedMessage == null)
            Thread.Sleep(100);

        // stop the server
        await server.Shutdown();
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

      // close the program on a key pressed
      Console.ReadKey();
    }

    private class CustomListener : IIsoMessageListener<IsoMessage>
    {
      private readonly IsoMessageFactory<IsoMessage> _messageFactory;

      public CustomListener(IsoMessageFactory<IsoMessage> messageFactory) => _messageFactory = messageFactory;

      public bool CanHandleMessage(IsoMessage isoMessage) => (int)isoMessage.Type == 0x1100;

      public bool HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage)
      {
        var t = isoMessage.Type.ToString("X4");
        // TODO remove this line when debugging is done
        Console.WriteLine($"{this.GetType()} handling message type {t}");
        
        // cache the received message
        _receivedMessage = isoMessage;
        var response = _messageFactory.CreateResponse(isoMessage);
        var field2 = isoMessage.GetField(2);
        var pan = field2.Value as string;
        response.CopyFieldsFrom(isoMessage, 2, 3, 4, 7, 11, 12, 37, 41, 42, 49);
        response.RemoveFields(13, 14, 19, 22, 24, 26, 35, 45);
        response.SetField(38, new IsoValue(IsoType.ALPHA, "123456", 6));
        response.SetField(39, new IsoValue(IsoType.NUMERIC, "000", 3));
        if (pan != null && pan.StartsWith("5")) // MasterCard
          response.SetField(15, new IsoValue(IsoType.DATE6, new DateTime()));
        // just for sample purpose
        context.WriteAndFlushAsync(response).RunSynchronously();
        return false;
      }
    }
  }
}