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

namespace Iso8583.Common.Validation
{
  /// <summary>
  ///   Validates an <see cref="IsoMessage"/> against a set of per-field rules. Rules are
  ///   registered through a fluent API and run in insertion order; every rule for every
  ///   configured field is executed so that callers see all errors in a single pass.
  ///
  ///   <para>
  ///   <b>Thread-safety:</b> the mutation methods (<see cref="ForField"/>, <see cref="AddRule"/>,
  ///   <see cref="Require"/>) are not thread-safe. Configure the validator fully before
  ///   attaching it to a <see cref="Iso8583.Common.ConnectorConfiguration.MessageValidator"/>
  ///   and starting the client/server. Once attached, call <see cref="Freeze"/> (or let the
  ///   pipeline run with the validator as-is): after <see cref="Freeze"/> has been called the
  ///   validator rejects any further mutation. The <see cref="Validate"/> method itself is
  ///   lock-free and safe for concurrent readers once mutation has stopped.
  ///   </para>
  /// </summary>
  /// <example>
  ///   <code>
  ///   var validator = new MessageValidator();
  ///   validator.ForField(2).AddRule(new LuhnValidator());
  ///   validator.ForField(49).AddRule(new CurrencyCodeValidator());
  ///   validator.Freeze(); // optional but recommended once configuration is complete
  ///
  ///   var report = validator.Validate(message);
  ///   if (!report.IsValid) { /* inspect report.Errors */ }
  ///   </code>
  /// </example>
  public sealed class MessageValidator
  {
    private readonly Dictionary<int, List<IFieldValidator>> _rules = new Dictionary<int, List<IFieldValidator>>();
    private readonly HashSet<int> _requiredFields = new HashSet<int>();
    private int _ruleCount;
    private bool _frozen;

    /// <summary>
    ///   Total number of <see cref="IFieldValidator"/> rules registered across all fields.
    ///   Does not count <see cref="Require"/> declarations.
    /// </summary>
    public int RuleCount => _ruleCount;

    /// <summary>
    ///   <c>true</c> after <see cref="Freeze"/> has been called. Frozen validators reject
    ///   further mutation so that pipeline-attached validators cannot be modified underneath
    ///   in-flight messages.
    /// </summary>
    public bool IsFrozen => _frozen;

    /// <summary>
    ///   Mark this validator as configuration-complete. Subsequent calls to <see cref="ForField"/>,
    ///   <see cref="AddRule"/>, or <see cref="Require"/> throw <see cref="InvalidOperationException"/>.
    ///   Freezing is idempotent.
    /// </summary>
    public MessageValidator Freeze()
    {
      _frozen = true;
      return this;
    }

    /// <summary>
    ///   Start configuring rules for the given ISO 8583 field number.
    /// </summary>
    public FieldRuleBuilder ForField(int fieldNumber)
    {
      ThrowIfFrozen();
      if (fieldNumber < 1 || fieldNumber > 128)
        throw new ArgumentOutOfRangeException(nameof(fieldNumber), "ISO 8583 field number must be in [1, 128]");
      return new FieldRuleBuilder(this, fieldNumber);
    }

    /// <summary>
    ///   Register a single validator for the given field. Prefer the fluent
    ///   <see cref="ForField"/> API when configuring multiple rules.
    /// </summary>
    public MessageValidator AddRule(int fieldNumber, IFieldValidator validator)
    {
      ThrowIfFrozen();
      if (fieldNumber < 1 || fieldNumber > 128)
        throw new ArgumentOutOfRangeException(nameof(fieldNumber), "ISO 8583 field number must be in [1, 128]");
      if (validator == null) throw new ArgumentNullException(nameof(validator));
      if (!_rules.TryGetValue(fieldNumber, out var list))
      {
        list = new List<IFieldValidator>();
        _rules[fieldNumber] = list;
      }
      list.Add(validator);
      _ruleCount++;
      return this;
    }

    /// <summary>
    ///   Mark a field as required. Required fields that are missing from a validated message
    ///   produce a failure in the resulting report, even when no other rules are registered.
    /// </summary>
    public MessageValidator Require(int fieldNumber)
    {
      ThrowIfFrozen();
      if (fieldNumber < 1 || fieldNumber > 128)
        throw new ArgumentOutOfRangeException(nameof(fieldNumber), "ISO 8583 field number must be in [1, 128]");
      _requiredFields.Add(fieldNumber);
      return this;
    }

    /// <summary>
    ///   Run every configured validator against the given message and return a report with
    ///   all failures. Fields that are absent from the message are skipped unless they were
    ///   marked via <see cref="Require"/>. The errors list is allocated lazily on the first
    ///   failure so the happy path is allocation-free aside from the returned
    ///   <see cref="ValidationReport.Valid"/> singleton.
    /// </summary>
    public ValidationReport Validate(IsoMessage message)
    {
      if (message == null) throw new ArgumentNullException(nameof(message));

      List<ValidationResult> errors = null;

      // Required fields first, so missing-field errors come before value errors.
      foreach (var fieldNumber in _requiredFields)
      {
        if (!message.HasField(fieldNumber))
          (errors ??= new List<ValidationResult>()).Add(ValidationResult.Failure(fieldNumber,
            $"Required field {fieldNumber} is missing", "RequiredField"));
      }

      foreach (var kv in _rules)
      {
        var fieldNumber = kv.Key;
        if (!message.HasField(fieldNumber)) continue;

        var isoValue = message.GetField(fieldNumber);
        if (isoValue == null) continue;

        var list = kv.Value;
        for (var i = 0; i < list.Count; i++)
        {
          var result = list[i].Validate(fieldNumber, isoValue);
          if (!result.IsValid)
            (errors ??= new List<ValidationResult>()).Add(result);
        }
      }

      return errors == null ? ValidationReport.Valid : new ValidationReport(errors);
    }

    private void ThrowIfFrozen()
    {
      if (_frozen)
        throw new InvalidOperationException(
          "MessageValidator has been frozen. Configure rules before attaching the validator to a connector configuration.");
    }

    /// <summary>
    ///   Fluent helper returned by <see cref="MessageValidator.ForField"/>.
    /// </summary>
    public sealed class FieldRuleBuilder
    {
      private readonly MessageValidator _owner;
      private readonly int _fieldNumber;

      internal FieldRuleBuilder(MessageValidator owner, int fieldNumber)
      {
        _owner = owner;
        _fieldNumber = fieldNumber;
      }

      /// <summary>Register a validator for this field.</summary>
      public FieldRuleBuilder AddRule(IFieldValidator validator)
      {
        _owner.AddRule(_fieldNumber, validator);
        return this;
      }

      /// <summary>Mark this field as required.</summary>
      public FieldRuleBuilder Required()
      {
        _owner.Require(_fieldNumber);
        return this;
      }

      /// <summary>Shortcut to continue configuring a different field.</summary>
      public FieldRuleBuilder ForField(int fieldNumber) => _owner.ForField(fieldNumber);
    }
  }
}
