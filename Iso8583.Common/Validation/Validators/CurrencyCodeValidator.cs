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

using System.Collections.Generic;
using NetCore8583;

namespace Iso8583.Common.Validation.Validators
{
  /// <summary>
  ///   Asserts that a field's value is a 3-digit ISO 4217 numeric currency code
  ///   (ISO 8583 fields 49/50/51). Only applicable to <see cref="IsoType.NUMERIC"/>;
  ///   other IsoTypes produce a clear failure. Callers that need a custom allow-list
  ///   can pass one to the constructor.
  /// </summary>
  public sealed class CurrencyCodeValidator : IFieldValidator
  {
    private const string Name = nameof(CurrencyCodeValidator);

    // Active ISO 4217 numeric codes. Source: ISO 4217:2015+ amendments (not exhaustive for historic codes).
    private static readonly HashSet<string> DefaultCodes = new HashSet<string>
    {
      "008", "012", "032", "036", "044", "048", "050", "051", "052", "060", "064", "068", "072",
      "084", "090", "096", "104", "108", "116", "124", "132", "136", "144", "152", "156", "170",
      "174", "188", "191", "192", "203", "208", "214", "222", "230", "232", "238", "242", "262",
      "270", "292", "320", "324", "328", "332", "340", "344", "348", "352", "356", "357", "360",
      "364", "368", "372", "376", "388", "392", "398", "400", "404", "408", "410", "414", "417",
      "418", "422", "426", "430", "434", "446", "454", "458", "462", "478", "480", "484", "496",
      "498", "504", "512", "516", "524", "532", "533", "548", "554", "558", "566", "578", "586",
      "590", "598", "600", "604", "608", "634", "643", "646", "654", "678", "682", "690", "694",
      "702", "704", "706", "710", "728", "748", "752", "756", "760", "764", "776", "780", "784",
      "788", "800", "807", "818", "826", "834", "840", "858", "860", "882", "886", "894", "901",
      "925", "926", "927", "928", "929", "930", "931", "932", "933", "934", "936", "938", "940",
      "941", "943", "944", "946", "947", "948", "949", "950", "951", "952", "953", "960", "961",
      "962", "963", "964", "965", "967", "968", "969", "970", "971", "972", "973", "975", "976",
      "977", "978", "979", "980", "981", "984", "985", "986", "990", "994", "997", "999"
    };

    private readonly HashSet<string> _allowed;

    /// <summary>
    ///   Create a currency code validator that accepts the default set of active ISO 4217 codes.
    /// </summary>
    public CurrencyCodeValidator()
    {
      _allowed = DefaultCodes;
    }

    /// <summary>
    ///   Create a currency code validator that only accepts the provided codes.
    /// </summary>
    public CurrencyCodeValidator(IEnumerable<string> allowedCodes)
    {
      _allowed = new HashSet<string>(allowedCodes);
    }

    /// <inheritdoc />
    public ValidationResult Validate(int fieldNumber, IsoValue value)
    {
      if (value == null)
        return ValidationResult.Failure(fieldNumber, "IsoValue is null", Name);

      if (value.Type != IsoType.NUMERIC)
        return ValidationResult.Failure(fieldNumber,
          $"CurrencyCodeValidator is not applicable to IsoType {value.Type}; expected NUMERIC", Name);

      var str = value.Value?.ToString() ?? string.Empty;
      if (str.Length != 3)
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' must be exactly 3 digits (ISO 4217 numeric)", Name);

      for (var i = 0; i < 3; i++)
        if (str[i] < '0' || str[i] > '9')
          return ValidationResult.Failure(fieldNumber,
            $"Value '{str}' must contain only digits (ISO 4217 numeric)", Name);

      if (!_allowed.Contains(str))
        return ValidationResult.Failure(fieldNumber,
          $"Value '{str}' is not a known ISO 4217 currency code", Name);

      return ValidationResult.Success(fieldNumber, Name);
    }
  }
}
