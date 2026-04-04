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
using System.Globalization;
using NetCore8583;

namespace Iso8583.Common.Validation.Validators
{
  /// <summary>
  ///   Validates that a field's value conforms to the date/time format implied by its
  ///   <see cref="IsoType"/>. Supports every NetCore8583 date type plus
  ///   <see cref="IsoType.TIME"/>:
  ///
  ///   <list type="table">
  ///     <listheader><term>IsoType</term><description>Format</description></listheader>
  ///     <item><term>DATE4</term><description><c>MMdd</c></description></item>
  ///     <item><term>DATE6</term><description><c>yyMMdd</c></description></item>
  ///     <item><term>DATE10</term><description><c>MMddHHmmss</c></description></item>
  ///     <item><term>DATE12</term><description><c>yyMMddHHmmss</c></description></item>
  ///     <item><term>DATE14</term><description><c>yyyyMMddHHmmss</c></description></item>
  ///     <item><term>DATE_EXP</term><description><c>yyMM</c></description></item>
  ///     <item><term>TIME</term><description><c>HHmmss</c></description></item>
  ///   </list>
  ///
  ///   Other IsoTypes produce a clear failure stating that the validator is not applicable.
  ///   Values that are already <see cref="DateTime"/> instances are accepted as valid.
  /// </summary>
  public sealed class DateValidator : IFieldValidator
  {
    private const string Name = nameof(DateValidator);

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      var format = FormatFor(value.Type);
      if (format == null)
        return ValidationResult.Failure(fieldNumber,
          $"DateValidator is not applicable to IsoType {value.Type}", Name);

      var raw = value.Value;
      if (raw == null)
        return ValidationResult.Failure(fieldNumber, "Value is null", Name);

      // NetCore8583 sometimes exposes dates as DateTime; accept those as valid.
      if (raw is DateTime)
        return ValidationResult.Success(fieldNumber, Name);

      var str = raw.ToString();
      if (string.IsNullOrEmpty(str))
        return ValidationResult.Failure(fieldNumber, "Value is empty", Name);

      if (!DateTime.TryParseExact(str, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' is not a valid {value.Type} (expected format '{format}')", Name);

      return ValidationResult.Success(fieldNumber, Name);
    }

    /// <summary>
    ///   Return the .NET format string that corresponds to the given IsoType, or <c>null</c>
    ///   when the type is not a date/time type.
    /// </summary>
    public static string FormatFor(IsoType type)
    {
      switch (type)
      {
        case IsoType.DATE4: return "MMdd";
        case IsoType.DATE6: return "yyMMdd";
        case IsoType.DATE10: return "MMddHHmmss";
        case IsoType.DATE12: return "yyMMddHHmmss";
        case IsoType.DATE14: return "yyyyMMddHHmmss";
        case IsoType.DATE_EXP: return "yyMM";
        case IsoType.TIME: return "HHmmss";
        default: return null;
      }
    }
  }
}
