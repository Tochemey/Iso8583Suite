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
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Shared helper that masks sensitive values in ISO 8583 messages. Used by both the
  ///   diagnostic <see cref="IsoMessageLoggingHandler"/> and the structured audit log
  ///   emitter so a single implementation governs what gets redacted.
  /// </summary>
  public static class SensitiveDataMasker
  {
    /// <summary>
    ///   Character used to overwrite the middle of a PAN and to build the generic masked value.
    /// </summary>
    public const char MaskChar = '*';

    /// <summary>
    ///   Default set of ISO 8583 fields whose raw value is replaced with <see cref="MaskedValueString"/>:
    ///   field 34 (PAN extended), 35 (track 2), 36 (track 3), 45 (track 1).
    /// </summary>
    public static readonly int[] DefaultMaskedFields = { 34, 35, 36, 45 };

    /// <summary>
    ///   String substituted for the value of fully masked fields.
    /// </summary>
    public const string MaskedValueString = "***";

    private static readonly char[] MaskedValueChars = MaskedValueString.ToCharArray();

    /// <summary>
    ///   Returns the character array substitute for fully masked field values.
    /// </summary>
    public static char[] MaskedValue() => MaskedValueChars;

    /// <summary>
    ///   Masks a PAN, keeping the first six and last four digits visible.
    /// </summary>
    /// <param name="fullPan">the unmasked PAN</param>
    /// <returns>the masked PAN as a character array</returns>
    public static char[] MaskPan(string fullPan)
    {
      if (fullPan == null) return Array.Empty<char>();
      var maskedPan = fullPan.ToCharArray();
      var unmaskedPrefix = Math.Min(6, maskedPan.Length);
      var unmaskedSuffix = Math.Min(4, Math.Max(0, maskedPan.Length - unmaskedPrefix));
      for (var i = unmaskedPrefix; i < maskedPan.Length - unmaskedSuffix; i++)
        maskedPan[i] = MaskChar;
      return maskedPan;
    }

    /// <summary>
    ///   Normalizes a caller-supplied masked field list: returns a sorted clone, or
    ///   <see cref="DefaultMaskedFields"/> when null/empty.
    /// </summary>
    public static int[] NormalizeMaskedFields(int[] maskedFields)
    {
      if (maskedFields is not { Length: > 0 }) return DefaultMaskedFields;
      var copy = (int[])maskedFields.Clone();
      Array.Sort(copy);
      return copy;
    }

    /// <summary>
    ///   Returns the masked string value for a single field, applying PAN masking on field 2
    ///   and full masking on any field present in <paramref name="maskedFields"/>.
    /// </summary>
    public static string MaskFieldValue(int fieldNumber, string rawValue, int[] maskedFields)
    {
      if (rawValue == null) return null;
      if (fieldNumber == 2) return new string(MaskPan(rawValue));
      var fields = maskedFields ?? DefaultMaskedFields;
      return Array.BinarySearch(fields, fieldNumber) >= 0 ? MaskedValueString : rawValue;
    }

    /// <summary>
    ///   Builds a masked dictionary of the present ISO 8583 fields keyed by field number as a
    ///   string. Suitable for emission as a structured audit property.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildMaskedFieldMap(IsoMessage message, int[] maskedFields)
    {
      var normalized = NormalizeMaskedFields(maskedFields);
      var result = new Dictionary<string, string>();
      for (var i = 2; i < 128; i++)
      {
        if (!message.HasField(i)) continue;
        var raw = message.GetField(i)?.ToString();
        result[i.ToString()] = MaskFieldValue(i, raw, normalized);
      }
      return result;
    }
  }
}
