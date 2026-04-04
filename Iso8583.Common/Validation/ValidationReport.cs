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
using System.Linq;
using System.Text;

namespace Iso8583.Common.Validation
{
  /// <summary>
  ///   Aggregated outcome of validating an ISO 8583 message. Contains the failure results
  ///   produced by every rule that did not pass. A report with no errors is considered valid.
  /// </summary>
  public sealed class ValidationReport
  {
    private static readonly IReadOnlyList<ValidationResult> EmptyErrors = new List<ValidationResult>();

    /// <summary>
    ///   A valid (empty) report.
    /// </summary>
    public static readonly ValidationReport Valid = new ValidationReport(EmptyErrors);

    /// <summary>
    ///   Create a new report containing the given errors.
    /// </summary>
    /// <param name="errors">Validation failures. Pass an empty collection for a successful report.</param>
    public ValidationReport(IReadOnlyList<ValidationResult> errors)
    {
      Errors = errors ?? EmptyErrors;
    }

    /// <summary>
    ///   All validation failures. Empty when <see cref="IsValid"/> is <c>true</c>.
    /// </summary>
    public IReadOnlyList<ValidationResult> Errors { get; }

    /// <summary>
    ///   <c>true</c> when no errors were collected.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    ///   Return all errors for a given field number.
    /// </summary>
    public IEnumerable<ValidationResult> ErrorsForField(int fieldNumber)
      => Errors.Where(e => e.FieldNumber == fieldNumber);

    /// <summary>
    ///   Produce a multi-line summary of all errors.
    /// </summary>
    public override string ToString()
    {
      if (IsValid) return "ValidationReport: OK";

      var sb = new StringBuilder();
      sb.Append("ValidationReport: ").Append(Errors.Count).AppendLine(" error(s)");
      foreach (var error in Errors)
      {
        sb.Append("  - Field ").Append(error.FieldNumber);
        if (!string.IsNullOrEmpty(error.ValidatorName))
          sb.Append(" [").Append(error.ValidatorName).Append(']');
        sb.Append(": ").AppendLine(error.ErrorMessage);
      }
      return sb.ToString();
    }
  }
}
