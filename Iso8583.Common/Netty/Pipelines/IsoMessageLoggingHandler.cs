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
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Channels;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Channel handler that logs ISO 8583 messages with human-readable field names.
  ///   Sensitive fields (PAN, track data) are masked unless <c>printSensitiveData</c> is enabled.
  /// </summary>
  public class IsoMessageLoggingHandler : LoggingHandler
  {
    private const char MaskChar = '*';

    /// <summary>
    ///   Default set of ISO 8583 fields that are masked in log output: PAN extended (34), track 2 (35), track 3 (36), track 1 (45).
    /// </summary>
    public static readonly int[] DefaultMaskedFields =
    {
      34, // PAN extended
      35, // track 2
      36, // track 3
      45 // track 1
    };

    private static readonly char[] MaskedValue = "***".ToCharArray();
    private static readonly Lazy<string[]> LazyFieldNames = new(LoadFieldNames);
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
    public IsoMessageLoggingHandler(LogLevel level,
      bool printSensitiveData = true,
      bool printFieldDescriptions = true,
      params int[] maskedFields) : base(level)
    {
      _printSensitiveData = printSensitiveData;
      _printFieldDescriptions = printFieldDescriptions;
      if (maskedFields is { Length: > 0 })
      {
        _maskedFields = (int[])maskedFields.Clone();
        Array.Sort(_maskedFields);
      }
      else
      {
        _maskedFields = DefaultMaskedFields;
      }
    }

    private static string[] LoadFieldNames()
    {
      var names = new string[128];
      var fields = Iso8583Fields.Fields;
      foreach (var key in fields.Keys)
      {
        if (int.TryParse(key, out var field) && field >= 1 && field <= 128)
          names[field - 1] = fields[key];
      }
      return names;
    }

    /// <inheritdoc />
    public override bool IsSharable => true;

    /// <summary>
    ///   Formats an event and returns the formatted message.
    /// </summary>
    /// <param name="ctx">the channel handler context</param>
    /// <param name="eventName">the name of the event</param>
    /// <param name="arg">the argument of the event</param>
    protected override string Format(IChannelHandlerContext ctx,
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
    internal string FormatIsoMessage(IsoMessage isoMessage)
    {
      var sb = new StringBuilder();
      if (_printSensitiveData) sb.Append("Message: ").Append(isoMessage.DebugString()).Append('\n');
      sb.Append("MTI: 0x").Append(isoMessage.Type.ToString("x4"));
      for (var i = 2; i < 128; i++)
        if (isoMessage.HasField(i))
        {
          var field = isoMessage.GetField(i);
          sb.Append("\n  ").Append(i).Append(": [");

          if (_printFieldDescriptions) sb.Append(LazyFieldNames.Value[i - 1]).Append(':');

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
      var unmaskedPrefix = Math.Min(6, maskedPan.Length);
      var unmaskedSuffix = Math.Min(4, Math.Max(0, maskedPan.Length - unmaskedPrefix));
      for (var i = unmaskedPrefix; i < maskedPan.Length - unmaskedSuffix; i++)
        maskedPan[i] = MaskChar;
      return maskedPan;
    }
  }
}