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

namespace Iso8583.Common.Validation
{
  /// <summary>
  ///   The outcome of validating a single field against a single rule.
  /// </summary>
  public readonly struct ValidationResult
  {
    private ValidationResult(bool isValid, int fieldNumber, string errorMessage, string validatorName)
    {
      IsValid = isValid;
      FieldNumber = fieldNumber;
      ErrorMessage = errorMessage;
      ValidatorName = validatorName;
    }

    /// <summary>
    ///   <c>true</c> when the field value satisfied the rule.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    ///   ISO 8583 field number that was validated.
    /// </summary>
    public int FieldNumber { get; }

    /// <summary>
    ///   Human-readable error message. <c>null</c> when <see cref="IsValid"/> is <c>true</c>.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    ///   Name of the validator that produced this result (useful for diagnostics).
    /// </summary>
    public string ValidatorName { get; }

    /// <summary>
    ///   Create a successful result.
    /// </summary>
    public static ValidationResult Success(int fieldNumber, string validatorName = null)
      => new ValidationResult(true, fieldNumber, null, validatorName);

    /// <summary>
    ///   Create a failure result.
    /// </summary>
    public static ValidationResult Failure(int fieldNumber, string errorMessage, string validatorName = null)
      => new ValidationResult(false, fieldNumber, errorMessage, validatorName);
  }
}
