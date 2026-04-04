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
  ///   Asserts that a field contains only ASCII digits. Applicable to the numeric
  ///   ISO 8583 types: <see cref="IsoType.NUMERIC"/> and <see cref="IsoType.AMOUNT"/>.
  ///   Using it on any other IsoType produces a clear failure describing the mismatch.
  /// </summary>
  public sealed class NumericValidator : IFieldValidator
  {
    private const string Name = nameof(NumericValidator);

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      if (value.Type != IsoType.NUMERIC && value.Type != IsoType.AMOUNT)
        return ValidationResult.Failure(fieldNumber,
          $"NumericValidator is not applicable to IsoType {value.Type}; expected NUMERIC or AMOUNT", Name);

      var str = value.Value?.ToString();
      if (string.IsNullOrEmpty(str))
        return ValidationResult.Failure(fieldNumber, "Value is empty", Name);

      for (var i = 0; i < str.Length; i++)
      {
        var c = str[i];
        if (c < '0' || c > '9')
          return ValidationResult.Failure(fieldNumber,
            $"Value '{str}' contains non-numeric character '{c}' at position {i}", Name);
      }

      return ValidationResult.Success(fieldNumber, Name);
    }
  }
}
