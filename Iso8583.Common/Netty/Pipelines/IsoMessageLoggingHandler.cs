using System;
using System.Text;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Channels;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  public class IsoMessageLoggingHandler : LoggingHandler
  {
    private const char MaskChar = '*';

    public static readonly int[] DefaultMaskedFields =
    {
      34, // PAN extended
      35, // track 2
      36, // track 3
      45 // track 1
    };

    private static readonly char[] MaskedValue = "***".ToCharArray();
    private static readonly string[] FieldNames = new string[128];
    private readonly int[] _maskedFields;
    private readonly bool _printFieldDescriptions;

    private readonly bool _printSensitiveData;

    /// <summary>
    ///   creates a new instance of the <see cref="IsoMessageLoggingHandler" />
    /// </summary>
    /// <param name="level">the log level</param>
    /// <param name="printSensitiveData">should print sensible data or not</param>
    /// <param name="printFieldDescriptions">should print fields descriptions or not</param>
    /// <param name="maskedFields">masked fields</param>
    /// <exception cref="Exception"></exception>
    public IsoMessageLoggingHandler(LogLevel level,
      bool printSensitiveData = true,
      bool printFieldDescriptions = true,
      params int[] maskedFields) : base(level)
    {
      var fields = Iso8583Fields.Fields();
      try
      {
        foreach (var key in fields.Keys)
        {
          var field = int.Parse(key);
          FieldNames[field - 1] = fields[key];
        }
      }
      catch (Exception e)
      {
        throw new Exception($"Unable to load ISO8583 field descriptions: Cause[{e}] ");
      }

      _printSensitiveData = printSensitiveData;
      _printFieldDescriptions = printFieldDescriptions;
      _maskedFields = maskedFields is {Length: > 0} ? maskedFields : DefaultMaskedFields;
    }

    public override bool IsSharable => true;

    /// <summary>
    ///   Formats an event and returns the formatted message.
    /// </summary>
    /// <param name="ctx">the channel handler context</param>
    /// <param name="eventName">the name of the event</param>
    /// <param name="arg">the argument of the event</param>
    protected new string Format(IChannelHandlerContext ctx,
      string eventName,
      object arg)
    {
      var message = arg as IsoMessage;
      return base.Format(ctx,
        eventName,
        message != null ? FormatIsoMessage(message) : arg);
    }


    /// <summary>
    ///   formats the iso message
    /// </summary>
    /// <param name="isoMessage">the iso message to format</param>
    /// <returns>the formatted iso message</returns>
    private string FormatIsoMessage(IsoMessage isoMessage)
    {
      var sb = new StringBuilder();
      if (_printSensitiveData) sb.Append("Message: ").Append(isoMessage.DebugString()).Append('\n');
      sb.Append("MTI: 0x").Append(isoMessage.Type.ToString("x4"));
      for (var i = 2; i < 128; i++)
        if (isoMessage.HasField(i))
        {
          var field = isoMessage.GetField(i);
          sb.Append("\n  ").Append(i).Append(": [");

          if (_printFieldDescriptions) sb.Append(FieldNames[i - 1]).Append(':');

          char[] formattedValue;
          if (_printSensitiveData)
            formattedValue = field.ToString().ToCharArray();
          else
            formattedValue = i switch
            {
              2 => MaskPan(field.ToString()),
              _ => Array.BinarySearch(_maskedFields, i) >= 0
                ? MaskedValue
                : field.ToString().ToCharArray()
            };

          sb.Append(field.Type).Append('(').Append(field.Length).Append(")] = '").Append(formattedValue)
            .Append('\'');
        }

      return sb.ToString();
    }


    /// <summary>
    ///   masks the PAN
    /// </summary>
    /// <param name="fullPan">the unmasked PAN value</param>
    /// <returns>the masked value</returns>
    private static char[] MaskPan(string fullPan)
    {
      var maskedPan = fullPan.ToCharArray();
      for (var i = 6; i < maskedPan.Length - 4; i++) maskedPan[i] = MaskChar;
      return maskedPan;
    }
  }
}