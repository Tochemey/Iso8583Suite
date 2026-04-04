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
using System.Text.RegularExpressions;
using NetCore8583;

namespace Iso8583.Common.Validation.Validators
{
  /// <summary>
  ///   Asserts that a field's string-serialised value matches a configured regular expression.
  ///   Applicable to textual IsoTypes: <see cref="IsoType.NUMERIC"/>, <see cref="IsoType.ALPHA"/>,
  ///   <see cref="IsoType.LLVAR"/>, <see cref="IsoType.LLLVAR"/>, <see cref="IsoType.LLLLVAR"/>.
  ///   Using it on binary or date IsoTypes returns a clear failure describing the mismatch.
  /// </summary>
  public sealed class RegexValidator : IFieldValidator
  {
    private const string Name = nameof(RegexValidator);
    private readonly Regex _regex;

    /// <summary>
    ///   Create a regex validator from a pattern string. The pattern is compiled once.
    /// </summary>
    public RegexValidator(string pattern)
    {
      if (string.IsNullOrEmpty(pattern))
        throw new ArgumentException("pattern must not be null or empty", nameof(pattern));
      _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>
    ///   Create a regex validator from an existing <see cref="Regex"/> instance.
    /// </summary>
    public RegexValidator(Regex regex)
    {
      _regex = regex ?? throw new ArgumentNullException(nameof(regex));
    }

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      if (!IsTextual(value.Type))
        return ValidationResult.Failure(fieldNumber,
          $"RegexValidator is not applicable to IsoType {value.Type}; expected a textual type", Name);

      var str = value.Value?.ToString() ?? string.Empty;
      if (!_regex.IsMatch(str))
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' does not match pattern '{_regex}'", Name);
      return ValidationResult.Success(fieldNumber, Name);
    }

    private static bool IsTextual(IsoType type)
      => type == IsoType.NUMERIC
         || type == IsoType.ALPHA
         || type == IsoType.LLVAR
         || type == IsoType.LLLVAR
         || type == IsoType.LLLLVAR;
  }
}
