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

using NetCore8583;

namespace Iso8583.Common.Validation.Validators
{
  /// <summary>
  ///   Asserts that a field's value passes the Luhn (mod 10) checksum. Typically used on
  ///   ISO 8583 field 2 (Primary Account Number), which is a variable-length text field in
  ///   every real-world scheme because PANs range from 12 to 19 digits. Applicable to the
  ///   variable-length text IsoTypes (<see cref="IsoType.LLVAR"/>, <see cref="IsoType.LLLVAR"/>,
  ///   <see cref="IsoType.LLLLVAR"/>). Non-digit characters cause validation to fail.
  /// </summary>
  public sealed class LuhnValidator : IFieldValidator
  {
    private const string Name = nameof(LuhnValidator);

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      if (!IsApplicable(value.Type))
        return ValidationResult.Failure(fieldNumber,
          $"LuhnValidator is not applicable to IsoType {value.Type}; expected LLVAR, LLLVAR or LLLLVAR", Name);

      var str = value.Value?.ToString();
      if (string.IsNullOrEmpty(str))
        return ValidationResult.Failure(fieldNumber, "Value is empty", Name);

      if (!TryComputeLuhn(str, out var valid))
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' contains non-digit characters", Name);

      if (!valid)
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' fails Luhn checksum", Name);

      return ValidationResult.Success(fieldNumber, Name);
    }

    private static bool IsApplicable(IsoType type)
      => type == IsoType.LLVAR
         || type == IsoType.LLLVAR
         || type == IsoType.LLLLVAR;

    /// <summary>
    ///   Compute the Luhn checksum for a string of digits.
    /// </summary>
    /// <param name="digits">The string to check.</param>
    /// <param name="valid">Set to <c>true</c> when all characters are digits and the checksum is valid.</param>
    /// <returns><c>false</c> when the input contains non-digit characters, <c>true</c> otherwise.</returns>
    public static bool TryComputeLuhn(string digits, out bool valid)
    {
      valid = false;
      if (string.IsNullOrEmpty(digits)) return false;

      var sum = 0;
      var alternate = false;
      for (var i = digits.Length - 1; i >= 0; i--)
      {
        var c = digits[i];
        if (c < '0' || c > '9') return false;
        var d = c - '0';
        if (alternate)
        {
          d *= 2;
          if (d > 9) d -= 9;
        }
        sum += d;
        alternate = !alternate;
      }

      valid = sum % 10 == 0;
      return true;
    }
  }
}
