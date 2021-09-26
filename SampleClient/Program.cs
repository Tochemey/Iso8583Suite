using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Client;
using Iso8583.Common;
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Parse;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace SampleClient
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
      const int serverPort = 9000;
      const string serverHost = "127.0.0.1";

      // create a message factory
      var mfact = ConfigParser.CreateDefault();
      mfact.UseBinaryMessages = false;
      mfact.Encoding = Encoding.ASCII;
      
      // let us create a message factory
      var messageFactory = new IsoMessageFactory<IsoMessage>(mfact,Iso8583Version.V1987);

      // let us configure the client
      var clientConfig = new ClientConfiguration
      {
        LogSensitiveData = true,
        ReplyOnError = true,
        IdleTimeout = 5,
        AddLoggingHandler = true
      };

      // configure the iso client
      var client = new Iso8583Client<IsoMessage>(clientConfig, messageFactory);

      // let us add some custom listener
      client.AddMessageListener(new CustomListener());

      // Establish a connection.
      // TODO: By default, if connection will is lost, it reconnects automatically.
      await client.Connect(serverHost, serverPort);

      // check whether the connection is established 
      if (!client.IsConnected())
      {
        _logger.Information("client is not connected. Shutting down");
        Environment.Exit(1);
      }

      _logger.Information("client is connected");
      const string pan = "5164123785712481";
      const string track2 = pan + "D17021011408011015360";
      const string track1 = "B" + pan + "^SUPPLIED/NOT^17021011408011015360";

      var message = messageFactory.NewMessage(0x1100);
      message.SetField(2, new IsoValue(IsoType.LLVAR, pan, pan.Length));
      message.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
      message.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
      message.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
      message.SetField(12, new IsoValue(IsoType.DATE12, DateTime.UtcNow));
      message.SetField(14, new IsoValue(IsoType.DATE_EXP,  new DateTime(2017, 2, 1)));
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

      // send the iso message to the iso server
      _logger.Debug("sending message {Type} to the server", message.Type.ToString("X4"));
      await client.Send(message);


      // let us wait for response
      while (_receivedMessage == null)
        Thread.Sleep(100);

      // disconnect 
      await client.Disconnect();

      // close the program on a key pressed
      Console.ReadKey();
    }

    private class CustomListener : IIsoMessageListener<IsoMessage>
    {
      public bool CanHandleMessage(IsoMessage isoMessage) => isoMessage.Type == 0x1110;

      public bool HandleMessage(IChannelHandlerContext context, IsoMessage isoMessage)
      {
        _receivedMessage = isoMessage;
        return false;
      }
    }
  }
}