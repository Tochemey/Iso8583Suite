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

namespace Iso8583.Common.Validation
{
  /// <summary>
  ///   Validates the value of a single ISO 8583 field. Built-in validators inspect the
  ///   <see cref="IsoValue.Type"/> and, where applicable, the declared
  ///   <see cref="IsoValue.Length"/> rather than accepting hand-wired length or format
  ///   parameters -- the idea being that the NetCore8583 field definition is already the
  ///   single source of truth for field geometry.
  /// </summary>
  public interface IFieldValidator
  {
    /// <summary>
    ///   Validate the <see cref="IsoValue"/> attached to <paramref name="fieldNumber"/>.
    /// </summary>
    /// <param name="fieldNumber">The ISO 8583 field number being validated.</param>
    /// <param name="value">The field value. Never <c>null</c>: absent fields are skipped by the <see cref="MessageValidator"/>.</param>
    /// <returns>A <see cref="ValidationResult"/> describing success or failure.</returns>
    ValidationResult Validate(int fieldNumber, IsoValue value);
  }
}
