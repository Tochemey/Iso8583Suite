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
using NetCore8583;

namespace Iso8583.Common.Validation.Validators
{
  /// <summary>
  ///   Validates the length of a field against the rules implied by its <see cref="IsoType"/>:
  ///
  ///   <list type="bullet">
  ///     <item><description>Fixed-length date/time types (DATE4, DATE6, DATE10, DATE12, DATE14, DATE_EXP, TIME) and AMOUNT: length must equal the IsoType's intrinsic length.</description></item>
  ///     <item><description>NUMERIC, ALPHA, BINARY: length must equal the declared <see cref="IsoValue.Length"/>.</description></item>
  ///     <item><description>LLVAR (text): length &lt;= min(99, declared length).</description></item>
  ///     <item><description>LLLVAR (text): length &lt;= min(999, declared length).</description></item>
  ///     <item><description>LLLLVAR (text): length &lt;= min(9999, declared length).</description></item>
  ///     <item><description>LLBIN/LLLBIN/LLLLBIN (binary): byte length bounded the same way.</description></item>
  ///   </list>
  ///
  ///   Use this validator without arguments; field geometry is taken entirely from the
  ///   NetCore8583 field definition attached to the <see cref="IsoValue"/>. NetCore8583 may
  ///   store date/time fields as <see cref="DateTime"/>; those are accepted without a length
  ///   check because NetCore8583 itself will format them to the correct width when encoding.
  /// </summary>
  public sealed class LengthValidator : IFieldValidator
  {
    private const string Name = nameof(LengthValidator);

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      if (value.Value == null)
        return ValidationResult.Failure(fieldNumber, "Value is null", Name);

      // NetCore8583 may expose date/time fields as DateTime. It will format the value to the
      // correct fixed width when encoding, so we accept it here without measuring.
      if (value.Value is DateTime && IsDateTimeType(value.Type))
        return ValidationResult.Success(fieldNumber, Name);

      if (!TryGetActualLength(value, out var actualLength))
        return ValidationResult.Failure(fieldNumber,
          $"Value length could not be determined for {value.Type}", Name);

      var declared = value.Length;

      switch (value.Type)
      {
        // Fixed-length types whose length is dictated by the IsoType itself.
        case IsoType.DATE4:
        case IsoType.DATE6:
        case IsoType.DATE10:
        case IsoType.DATE12:
        case IsoType.DATE14:
        case IsoType.DATE_EXP:
        case IsoType.TIME:
        case IsoType.AMOUNT:
        {
          var expected = value.Type.Length();
          return actualLength == expected
            ? ValidationResult.Success(fieldNumber, Name)
            : ValidationResult.Failure(fieldNumber,
              $"Length {actualLength} does not match fixed length {expected} required by {value.Type}", Name);
        }

        // Fixed-length types whose length is taken from the declared IsoValue.Length.
        case IsoType.NUMERIC:
        case IsoType.ALPHA:
        case IsoType.BINARY:
        {
          return actualLength == declared
            ? ValidationResult.Success(fieldNumber, Name)
            : ValidationResult.Failure(fieldNumber,
              $"Length {actualLength} does not match declared length {declared} for {value.Type}", Name);
        }

        // Variable-length text types. Max = min(protocol max, declared length).
        case IsoType.LLVAR:
          return CheckMaxLength(fieldNumber, actualLength, declared, 99, value.Type);
        case IsoType.LLLVAR:
          return CheckMaxLength(fieldNumber, actualLength, declared, 999, value.Type);
        case IsoType.LLLLVAR:
          return CheckMaxLength(fieldNumber, actualLength, declared, 9999, value.Type);

        // Variable-length binary types.
        case IsoType.LLBIN:
          return CheckMaxLength(fieldNumber, actualLength, declared, 99, value.Type);
        case IsoType.LLLBIN:
          return CheckMaxLength(fieldNumber, actualLength, declared, 999, value.Type);
        case IsoType.LLLLBIN:
          return CheckMaxLength(fieldNumber, actualLength, declared, 9999, value.Type);

        default:
          return ValidationResult.Failure(fieldNumber,
            $"Unsupported IsoType {value.Type}", Name);
      }
    }

    private static ValidationResult CheckMaxLength(int fieldNumber, int actual, int declared, int protocolMax, IsoType type)
    {
      // When no declared length is given (0), fall back to the protocol maximum.
      var max = declared > 0 && declared < protocolMax ? declared : protocolMax;
      if (actual > max)
        return ValidationResult.Failure(fieldNumber,
          $"Length {actual} exceeds maximum {max} for {type}", Name);
      return ValidationResult.Success(fieldNumber, Name);
    }

    /// <summary>
    ///   Derive the "length" of an <see cref="IsoValue"/> for the purpose of validation.
    ///   Binary types are measured in bytes (including an odd-length-hex rejection);
    ///   everything else in characters via ToString().
    /// </summary>
    private static bool TryGetActualLength(IsoValue value, out int length)
    {
      var raw = value.Value;

      switch (value.Type)
      {
        case IsoType.BINARY:
        case IsoType.LLBIN:
        case IsoType.LLLBIN:
        case IsoType.LLLLBIN:
          if (raw is byte[] bytes)
          {
            length = bytes.Length;
            return true;
          }
          if (raw is sbyte[] sbytes)
          {
            length = sbytes.Length;
            return true;
          }
          // Fall back to string length for hex-encoded representations.
          var hex = raw.ToString();
          if (hex == null || (hex.Length & 1) == 1)
          {
            length = 0;
            return false;
          }
          length = hex.Length / 2;
          return true;

        default:
          var str = raw.ToString();
          length = str?.Length ?? 0;
          return str != null;
      }
    }

    private static bool IsDateTimeType(IsoType type)
    {
      switch (type)
      {
        case IsoType.DATE4:
        case IsoType.DATE6:
        case IsoType.DATE10:
        case IsoType.DATE12:
        case IsoType.DATE14:
        case IsoType.DATE_EXP:
        case IsoType.TIME:
          return true;
        default:
          return false;
      }
    }
  }
}
