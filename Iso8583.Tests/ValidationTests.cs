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
using System.Linq;
using System.Text;
using Iso8583.Common.Iso;
using Iso8583.Common.Validation;
using Iso8583.Common.Validation.Validators;
using NetCore8583;
using NetCore8583.Parse;
using Xunit;

namespace Iso8583.Tests;

public class ValidationTests
{
    private readonly IsoMessageFactory<IsoMessage> _factory;

    public ValidationTests()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);
    }

    private static IsoValue Iso(IsoType type, object value, int length = 0) => new IsoValue(type, value, length);

    // ==================== LengthValidator ====================

    // ---- Fixed-length IsoTypes driven by IsoType.Length() ----

    [Theory]
    [InlineData(IsoType.DATE4, "1225", 4)]
    [InlineData(IsoType.DATE6, "261225", 6)]
    [InlineData(IsoType.DATE10, "1225153045", 10)]
    [InlineData(IsoType.DATE12, "261225153045", 12)]
    [InlineData(IsoType.DATE14, "20261225153045", 14)]
    [InlineData(IsoType.DATE_EXP, "2612", 4)]
    [InlineData(IsoType.TIME, "153045", 6)]
    [InlineData(IsoType.AMOUNT, "000000001000", 12)]
    public void LengthValidator_FixedByType_ExactMatch_Succeeds(IsoType type, string value, int declared)
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(1, Iso(type, value, declared)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.DATE4, "12")]
    [InlineData(IsoType.DATE6, "12345")]
    [InlineData(IsoType.DATE10, "12345678")]
    [InlineData(IsoType.DATE12, "12345678")]
    [InlineData(IsoType.DATE14, "12345678")]
    [InlineData(IsoType.DATE_EXP, "12")]
    [InlineData(IsoType.TIME, "12")]
    [InlineData(IsoType.AMOUNT, "100")]
    public void LengthValidator_FixedByType_Mismatch_Fails(IsoType type, string value)
    {
        var v = new LengthValidator();
        var result = v.Validate(1, Iso(type, value, 0));
        Assert.False(result.IsValid);
        Assert.Contains("fixed length", result.ErrorMessage);
    }

    // ---- Fixed-length IsoTypes driven by declared IsoValue.Length ----

    [Fact]
    public void LengthValidator_Numeric_ExactDeclaredLength_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(3, Iso(IsoType.NUMERIC, "004000", 6)).IsValid);
    }

    [Fact]
    public void LengthValidator_Numeric_MismatchDeclaredLength_Fails()
    {
        var v = new LengthValidator();
        var result = v.Validate(3, Iso(IsoType.NUMERIC, "4000", 6));
        Assert.False(result.IsValid);
        Assert.Contains("declared length 6", result.ErrorMessage);
    }

    [Fact]
    public void LengthValidator_Alpha_ExactDeclaredLength_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(41, Iso(IsoType.ALPHA, "TERM0001", 8)).IsValid);
    }

    [Fact]
    public void LengthValidator_Alpha_Mismatch_Fails()
    {
        var v = new LengthValidator();
        Assert.False(v.Validate(41, Iso(IsoType.ALPHA, "SHORT", 8)).IsValid);
    }

    [Fact]
    public void LengthValidator_Binary_ExactByteLength_Succeeds()
    {
        var v = new LengthValidator();
        var bytes = new byte[] { 1, 2, 3, 4 };
        Assert.True(v.Validate(64, Iso(IsoType.BINARY, bytes, 4)).IsValid);
    }

    [Fact]
    public void LengthValidator_Binary_SByteArray_Supported()
    {
        var v = new LengthValidator();
        var sbytes = new sbyte[] { 1, 2, 3 };
        Assert.True(v.Validate(64, Iso(IsoType.BINARY, sbytes, 3)).IsValid);
    }

    [Fact]
    public void LengthValidator_Binary_HexString_Fallback()
    {
        var v = new LengthValidator();
        // 8 hex chars => 4 bytes
        Assert.True(v.Validate(64, Iso(IsoType.BINARY, "01020304", 4)).IsValid);
    }

    [Fact]
    public void LengthValidator_Binary_Mismatch_Fails()
    {
        var v = new LengthValidator();
        var bytes = new byte[] { 1, 2 };
        Assert.False(v.Validate(64, Iso(IsoType.BINARY, bytes, 4)).IsValid);
    }

    // ---- Variable-length text types ----

    [Fact]
    public void LengthValidator_LLVAR_WithinDeclaredMax_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(2, Iso(IsoType.LLVAR, "4111111111111111", 19)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLVAR_ExceedsDeclaredMax_Fails()
    {
        var v = new LengthValidator();
        var result = v.Validate(2, Iso(IsoType.LLVAR, "41111111111111119999", 16));
        Assert.False(result.IsValid);
        Assert.Contains("exceeds maximum 16", result.ErrorMessage);
    }

    [Fact]
    public void LengthValidator_LLVAR_DeclaredZero_UsesProtocolMax()
    {
        // When declared=0, the validator falls back to the LLVAR protocol max of 99.
        // NetCore8583's IsoValue constructor also enforces that max, so we can only
        // verify the happy path up to 99 chars here.
        var v = new LengthValidator();
        var max99 = new string('9', 99);
        Assert.True(v.Validate(2, Iso(IsoType.LLVAR, max99, 0)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLVAR_WithinProtocolMax_Succeeds()
    {
        var v = new LengthValidator();
        var s = new string('x', 500);
        Assert.True(v.Validate(48, Iso(IsoType.LLLVAR, s, 999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLVAR_Exceeds_Fails()
    {
        var v = new LengthValidator();
        var s = new string('x', 1000);
        Assert.False(v.Validate(48, Iso(IsoType.LLLVAR, s, 999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLLVAR_WithinProtocolMax_Succeeds()
    {
        var v = new LengthValidator();
        var s = new string('x', 5000);
        Assert.True(v.Validate(104, Iso(IsoType.LLLLVAR, s, 9999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLLVAR_Exceeds_Fails()
    {
        var v = new LengthValidator();
        var s = new string('x', 10000);
        Assert.False(v.Validate(104, Iso(IsoType.LLLLVAR, s, 9999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLBIN_WithinMax_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(55, Iso(IsoType.LLBIN, new byte[50], 99)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLBIN_Exceeds_Fails()
    {
        var v = new LengthValidator();
        Assert.False(v.Validate(55, Iso(IsoType.LLBIN, new byte[20], 10)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLBIN_WithinMax_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(55, Iso(IsoType.LLLBIN, new byte[500], 999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLBIN_Exceeds_Fails()
    {
        var v = new LengthValidator();
        Assert.False(v.Validate(55, Iso(IsoType.LLLBIN, new byte[1000], 999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLLBIN_WithinMax_Succeeds()
    {
        var v = new LengthValidator();
        Assert.True(v.Validate(55, Iso(IsoType.LLLLBIN, new byte[5000], 9999)).IsValid);
    }

    [Fact]
    public void LengthValidator_LLLLBIN_Exceeds_Fails()
    {
        var v = new LengthValidator();
        Assert.False(v.Validate(55, Iso(IsoType.LLLLBIN, new byte[10000], 9999)).IsValid);
    }

    [Fact]
    public void LengthValidator_NullIsoValue_Fails()
    {
        var v = new LengthValidator();
        var result = v.Validate(1, null);
        Assert.False(result.IsValid);
        Assert.Contains("null", result.ErrorMessage);
    }

    [Fact]
    public void LengthValidator_NullUnderlyingValue_FailsWithClearMessage()
    {
        var v = new LengthValidator();
        var result = v.Validate(3, Iso(IsoType.NUMERIC, null, 6));
        Assert.False(result.IsValid);
        Assert.Contains("Value is null", result.ErrorMessage);
    }

    [Theory]
    [InlineData(IsoType.DATE4)]
    [InlineData(IsoType.DATE6)]
    [InlineData(IsoType.DATE10)]
    [InlineData(IsoType.DATE12)]
    [InlineData(IsoType.DATE14)]
    [InlineData(IsoType.DATE_EXP)]
    [InlineData(IsoType.TIME)]
    public void LengthValidator_DateTimeValue_AcceptedForDateTypes(IsoType type)
    {
        // When NetCore8583 stores a date/time field as a DateTime (not a pre-formatted string),
        // the validator must accept it: NetCore8583 itself will format it to the correct width.
        var v = new LengthValidator();
        var result = v.Validate(7, Iso(type, new DateTime(2026, 4, 5, 12, 34, 56), 0));
        Assert.True(result.IsValid, result.ErrorMessage);
    }

    [Fact]
    public void LengthValidator_Binary_OddHexString_Fails()
    {
        // An odd number of hex characters cannot be a valid byte sequence.
        var v = new LengthValidator();
        var result = v.Validate(64, Iso(IsoType.BINARY, "010", 4));
        Assert.False(result.IsValid);
        Assert.Contains("could not be determined", result.ErrorMessage);
    }

    // ==================== NumericValidator ====================

    [Fact]
    public void NumericValidator_Numeric_AllDigits_Succeeds()
    {
        var v = new NumericValidator();
        Assert.True(v.Validate(4, Iso(IsoType.NUMERIC, "000000100000", 12)).IsValid);
    }

    [Fact]
    public void NumericValidator_Amount_AllDigits_Succeeds()
    {
        var v = new NumericValidator();
        Assert.True(v.Validate(4, Iso(IsoType.AMOUNT, "000000001000", 12)).IsValid);
    }

    [Fact]
    public void NumericValidator_ContainsLetter_Fails()
    {
        var v = new NumericValidator();
        var result = v.Validate(4, Iso(IsoType.NUMERIC, "12a45", 5));
        Assert.False(result.IsValid);
        Assert.Contains("non-numeric", result.ErrorMessage);
    }

    [Fact]
    public void NumericValidator_NullUnderlyingValue_Fails()
    {
        var v = new NumericValidator();
        Assert.False(v.Validate(4, Iso(IsoType.NUMERIC, null, 6)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.ALPHA)]
    [InlineData(IsoType.LLVAR)]
    [InlineData(IsoType.LLLVAR)]
    [InlineData(IsoType.LLLLVAR)]
    [InlineData(IsoType.BINARY)]
    [InlineData(IsoType.DATE4)]
    [InlineData(IsoType.TIME)]
    public void NumericValidator_WrongIsoType_Fails(IsoType type)
    {
        var v = new NumericValidator();
        var result = v.Validate(1, Iso(type, "123", 3));
        Assert.False(result.IsValid);
        Assert.Contains("not applicable", result.ErrorMessage);
    }

    [Fact]
    public void NumericValidator_NullIsoValue_Fails()
    {
        Assert.False(new NumericValidator().Validate(1, null).IsValid);
    }

    // ==================== DateValidator ====================

    [Theory]
    [InlineData(IsoType.DATE4, "1225")]
    [InlineData(IsoType.DATE6, "261225")]
    [InlineData(IsoType.DATE10, "1225153045")]
    [InlineData(IsoType.DATE12, "261225153045")]
    [InlineData(IsoType.DATE14, "20261225153045")]
    [InlineData(IsoType.DATE_EXP, "2612")]
    [InlineData(IsoType.TIME, "153045")]
    public void DateValidator_ValidFormats_Succeed(IsoType type, string value)
    {
        var v = new DateValidator();
        Assert.True(v.Validate(7, Iso(type, value, 0)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.DATE4, "1325")]  // month 13
    [InlineData(IsoType.DATE6, "261325")] // month 13
    [InlineData(IsoType.DATE10, "1225253045")] // hour 25
    [InlineData(IsoType.DATE12, "261225253045")]
    [InlineData(IsoType.DATE14, "20261225253045")]
    [InlineData(IsoType.DATE_EXP, "2613")]
    [InlineData(IsoType.TIME, "253045")]
    public void DateValidator_InvalidValues_Fail(IsoType type, string value)
    {
        var v = new DateValidator();
        var result = v.Validate(7, Iso(type, value, 0));
        Assert.False(result.IsValid);
        Assert.Contains("not a valid", result.ErrorMessage);
    }

    [Fact]
    public void DateValidator_DateTimeValue_Succeeds()
    {
        var v = new DateValidator();
        Assert.True(v.Validate(7, Iso(IsoType.DATE10, DateTime.Now, 0)).IsValid);
    }

    [Fact]
    public void DateValidator_NullUnderlyingValue_Fails()
    {
        Assert.False(new DateValidator().Validate(7, Iso(IsoType.DATE4, null, 0)).IsValid);
    }

    [Fact]
    public void DateValidator_EmptyString_Fails()
    {
        Assert.False(new DateValidator().Validate(7, Iso(IsoType.DATE4, "", 0)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.NUMERIC)]
    [InlineData(IsoType.ALPHA)]
    [InlineData(IsoType.LLVAR)]
    [InlineData(IsoType.LLLVAR)]
    [InlineData(IsoType.LLLLVAR)]
    [InlineData(IsoType.BINARY)]
    [InlineData(IsoType.AMOUNT)]
    public void DateValidator_NonDateIsoType_Fails(IsoType type)
    {
        var v = new DateValidator();
        var result = v.Validate(1, Iso(type, "1225", 4));
        Assert.False(result.IsValid);
        Assert.Contains("not applicable", result.ErrorMessage);
    }

    [Fact]
    public void DateValidator_NullIsoValue_Fails()
    {
        Assert.False(new DateValidator().Validate(7, null).IsValid);
    }

    [Fact]
    public void DateValidator_FormatFor_ReturnsNullForNonDateType()
    {
        Assert.Null(DateValidator.FormatFor(IsoType.NUMERIC));
        Assert.Equal("MMdd", DateValidator.FormatFor(IsoType.DATE4));
    }

    // ==================== LuhnValidator ====================

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("5555555555554444")]
    [InlineData("378282246310005")]
    public void LuhnValidator_ValidPan_Succeeds(string pan)
    {
        var v = new LuhnValidator();
        Assert.True(v.Validate(2, Iso(IsoType.LLVAR, pan, 19)).IsValid);
    }

    [Fact]
    public void LuhnValidator_InvalidChecksum_Fails()
    {
        var v = new LuhnValidator();
        var result = v.Validate(2, Iso(IsoType.LLVAR, "4111111111111112", 19));
        Assert.False(result.IsValid);
        Assert.Contains("Luhn", result.ErrorMessage);
    }

    [Fact]
    public void LuhnValidator_NonDigit_Fails()
    {
        var v = new LuhnValidator();
        var result = v.Validate(2, Iso(IsoType.LLVAR, "4111-1111-1111-1111", 19));
        Assert.False(result.IsValid);
        Assert.Contains("non-digit", result.ErrorMessage);
    }

    [Fact]
    public void LuhnValidator_EmptyValue_Fails()
    {
        var v = new LuhnValidator();
        Assert.False(v.Validate(2, Iso(IsoType.LLVAR, "", 19)).IsValid);
    }

    [Fact]
    public void LuhnValidator_NullUnderlyingValue_Fails()
    {
        var v = new LuhnValidator();
        Assert.False(v.Validate(2, Iso(IsoType.LLVAR, null, 19)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.LLVAR)]
    [InlineData(IsoType.LLLVAR)]
    [InlineData(IsoType.LLLLVAR)]
    public void LuhnValidator_SupportedTypes_AcceptValidPan(IsoType type)
    {
        var v = new LuhnValidator();
        Assert.True(v.Validate(2, Iso(type, "4111111111111111", 19)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.NUMERIC)] // PAN is variable-length in practice, not fixed NUMERIC
    [InlineData(IsoType.ALPHA)]
    [InlineData(IsoType.BINARY)]
    [InlineData(IsoType.DATE4)]
    [InlineData(IsoType.AMOUNT)]
    [InlineData(IsoType.TIME)]
    public void LuhnValidator_UnsupportedType_Fails(IsoType type)
    {
        var v = new LuhnValidator();
        var result = v.Validate(2, Iso(type, "4111111111111111", 16));
        Assert.False(result.IsValid);
        Assert.Contains("not applicable", result.ErrorMessage);
    }

    [Fact]
    public void LuhnValidator_NullIsoValue_Fails()
    {
        Assert.False(new LuhnValidator().Validate(2, null).IsValid);
    }

    [Fact]
    public void LuhnValidator_TryComputeLuhn_DirectCall()
    {
        Assert.True(LuhnValidator.TryComputeLuhn("4111111111111111", out var valid) && valid);
        Assert.True(LuhnValidator.TryComputeLuhn("4111111111111112", out var v2) && !v2);
        Assert.False(LuhnValidator.TryComputeLuhn("abcd", out _));
        Assert.False(LuhnValidator.TryComputeLuhn("", out _));
    }

    // ==================== CurrencyCodeValidator ====================

    [Theory]
    [InlineData("840")] // USD
    [InlineData("978")] // EUR
    [InlineData("826")] // GBP
    [InlineData("392")] // JPY
    [InlineData("999")] // No currency
    public void CurrencyCodeValidator_KnownCode_Succeeds(string code)
    {
        var v = new CurrencyCodeValidator();
        Assert.True(v.Validate(49, Iso(IsoType.NUMERIC, code, 3)).IsValid);
    }

    [Fact]
    public void CurrencyCodeValidator_UnknownCode_Fails()
    {
        var v = new CurrencyCodeValidator();
        var result = v.Validate(49, Iso(IsoType.NUMERIC, "000", 3));
        Assert.False(result.IsValid);
        Assert.Contains("not a known ISO 4217", result.ErrorMessage);
    }

    [Fact]
    public void CurrencyCodeValidator_NonDigit_Fails()
    {
        var v = new CurrencyCodeValidator();
        var result = v.Validate(49, Iso(IsoType.NUMERIC, "USD", 3));
        Assert.False(result.IsValid);
        Assert.Contains("only digits", result.ErrorMessage);
    }

    [Fact]
    public void CurrencyCodeValidator_WrongLength_Fails()
    {
        var v = new CurrencyCodeValidator();
        Assert.False(v.Validate(49, Iso(IsoType.NUMERIC, "84", 3)).IsValid);
        Assert.False(v.Validate(49, Iso(IsoType.NUMERIC, "8400", 3)).IsValid);
    }

    [Fact]
    public void CurrencyCodeValidator_CustomAllowList_Works()
    {
        var v = new CurrencyCodeValidator(new[] { "001", "002" });
        Assert.True(v.Validate(49, Iso(IsoType.NUMERIC, "001", 3)).IsValid);
        Assert.False(v.Validate(49, Iso(IsoType.NUMERIC, "840", 3)).IsValid);
    }

    [Theory]
    [InlineData(IsoType.ALPHA)]
    [InlineData(IsoType.LLVAR)]
    [InlineData(IsoType.AMOUNT)]
    public void CurrencyCodeValidator_NonNumericType_Fails(IsoType type)
    {
        var v = new CurrencyCodeValidator();
        var result = v.Validate(49, Iso(type, "840", 3));
        Assert.False(result.IsValid);
        Assert.Contains("not applicable", result.ErrorMessage);
    }

    [Fact]
    public void CurrencyCodeValidator_NullIsoValue_Fails()
    {
        Assert.False(new CurrencyCodeValidator().Validate(49, null).IsValid);
    }

    // ==================== RegexValidator ====================

    [Fact]
    public void RegexValidator_Match_Succeeds()
    {
        var v = new RegexValidator("^[A-Z]{3}$");
        Assert.True(v.Validate(41, Iso(IsoType.ALPHA, "TRM", 3)).IsValid);
    }

    [Fact]
    public void RegexValidator_NoMatch_Fails()
    {
        var v = new RegexValidator("^[A-Z]{3}$");
        Assert.False(v.Validate(41, Iso(IsoType.ALPHA, "trm", 3)).IsValid);
    }

    [Fact]
    public void RegexValidator_FromRegexInstance_Works()
    {
        var v = new RegexValidator(new System.Text.RegularExpressions.Regex(@"^\d+$"));
        Assert.True(v.Validate(3, Iso(IsoType.NUMERIC, "1234", 4)).IsValid);
    }

    [Fact]
    public void RegexValidator_NullPattern_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RegexValidator((string)null));
        Assert.Throws<ArgumentException>(() => new RegexValidator(""));
    }

    [Fact]
    public void RegexValidator_NullRegex_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RegexValidator((System.Text.RegularExpressions.Regex)null));
    }

    [Theory]
    [InlineData(IsoType.BINARY)]
    [InlineData(IsoType.DATE4)]
    [InlineData(IsoType.AMOUNT)]
    [InlineData(IsoType.TIME)]
    [InlineData(IsoType.LLBIN)]
    public void RegexValidator_NonTextualType_Fails(IsoType type)
    {
        var v = new RegexValidator("^.*$");
        var result = v.Validate(1, Iso(type, "abc", 3));
        Assert.False(result.IsValid);
        Assert.Contains("not applicable", result.ErrorMessage);
    }

    [Fact]
    public void RegexValidator_NullIsoValue_Fails()
    {
        Assert.False(new RegexValidator("^.*$").Validate(1, null).IsValid);
    }

    // ==================== MessageValidator ====================

    [Fact]
    public void MessageValidator_FluentApi_RegistersRules()
    {
        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator()).AddRule(new LengthValidator());
        validator.ForField(49).AddRule(new CurrencyCodeValidator());

        Assert.Equal(3, validator.RuleCount);
    }

    [Fact]
    public void MessageValidator_ValidMessage_ReportIsValid()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111111", 16));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000001000", 12));
        msg.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator()).AddRule(new LengthValidator());
        validator.ForField(4).AddRule(new NumericValidator()).AddRule(new LengthValidator());
        validator.ForField(49).AddRule(new CurrencyCodeValidator());

        var report = validator.Validate(msg);
        Assert.True(report.IsValid, report.ToString());
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void MessageValidator_InvalidPan_ReportsError()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111112", 16));

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.False(report.IsValid);
        Assert.Single(report.Errors);
        Assert.Equal(2, report.Errors[0].FieldNumber);
        Assert.Equal("LuhnValidator", report.Errors[0].ValidatorName);
    }

    [Fact]
    public void MessageValidator_CollectsMultipleErrors()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111112", 16));
        msg.SetField(49, new IsoValue(IsoType.NUMERIC, "000", 3));

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());
        validator.ForField(49).AddRule(new CurrencyCodeValidator());

        var report = validator.Validate(msg);
        Assert.False(report.IsValid);
        Assert.Equal(2, report.Errors.Count);
        Assert.Contains(report.Errors, e => e.FieldNumber == 2);
        Assert.Contains(report.Errors, e => e.FieldNumber == 49);
    }

    [Fact]
    public void MessageValidator_AbsentField_SkippedWhenNotRequired()
    {
        var msg = _factory.NewMessage(0x0200);

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void MessageValidator_RequiredField_Missing_ReportsError()
    {
        var msg = _factory.NewMessage(0x0200);

        var validator = new MessageValidator();
        validator.ForField(2).Required().AddRule(new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.False(report.IsValid);
        Assert.Single(report.Errors);
        Assert.Equal(2, report.Errors[0].FieldNumber);
        Assert.Contains("missing", report.Errors[0].ErrorMessage);
    }

    [Fact]
    public void MessageValidator_RequiredField_Present_ValidatesRules()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111111", 16));

        var validator = new MessageValidator();
        validator.ForField(2).Required().AddRule(new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void MessageValidator_DirectRequireMethod_Works()
    {
        var msg = _factory.NewMessage(0x0200);
        var validator = new MessageValidator();
        validator.Require(2);

        var report = validator.Validate(msg);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void MessageValidator_DirectAddRuleMethod_Works()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111112", 16));

        var validator = new MessageValidator();
        validator.AddRule(2, new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.False(report.IsValid);
    }

    [Fact]
    public void MessageValidator_FieldRuleBuilder_ChainToAnotherField()
    {
        var validator = new MessageValidator();
        validator
            .ForField(2).AddRule(new LuhnValidator())
            .ForField(49).AddRule(new CurrencyCodeValidator());
        Assert.Equal(2, validator.RuleCount);
    }

    [Fact]
    public void MessageValidator_NullMessage_Throws()
    {
        var validator = new MessageValidator();
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null));
    }

    [Fact]
    public void MessageValidator_InvalidFieldNumber_Throws()
    {
        var validator = new MessageValidator();
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.ForField(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.ForField(129));
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.Require(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.Require(129));
    }

    [Fact]
    public void MessageValidator_AddRule_NullValidator_Throws()
    {
        var validator = new MessageValidator();
        Assert.Throws<ArgumentNullException>(() => validator.AddRule(2, null));
    }

    [Fact]
    public void MessageValidator_AddRule_InvalidFieldNumber_Throws()
    {
        var validator = new MessageValidator();
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.AddRule(0, new LuhnValidator()));
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.AddRule(129, new LuhnValidator()));
    }

    [Fact]
    public void MessageValidator_Freeze_MakesFurtherMutationThrow()
    {
        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());
        Assert.False(validator.IsFrozen);

        validator.Freeze();
        Assert.True(validator.IsFrozen);

        Assert.Throws<InvalidOperationException>(() => validator.ForField(49));
        Assert.Throws<InvalidOperationException>(() => validator.AddRule(49, new CurrencyCodeValidator()));
        Assert.Throws<InvalidOperationException>(() => validator.Require(49));
    }

    [Fact]
    public void MessageValidator_Freeze_IsIdempotent()
    {
        var validator = new MessageValidator();
        validator.Freeze();
        validator.Freeze(); // should not throw
        Assert.True(validator.IsFrozen);
    }

    [Fact]
    public void MessageValidator_Freeze_DoesNotAffectValidate()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111111", 16));

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());
        validator.Freeze();

        var report = validator.Validate(msg);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void MessageValidator_ValidateHappyPath_ReturnsSharedValidSingleton()
    {
        // The lazy-allocation optimisation means a message with no failures returns the shared
        // ValidationReport.Valid instance rather than a freshly allocated report.
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "4111111111111111", 16));

        var validator = new MessageValidator();
        validator.ForField(2).AddRule(new LuhnValidator());

        var report = validator.Validate(msg);
        Assert.Same(ValidationReport.Valid, report);
    }

    [Fact]
    public void MessageValidator_RuleCount_IncrementsOnEachAdd()
    {
        var validator = new MessageValidator();
        Assert.Equal(0, validator.RuleCount);
        validator.ForField(2).AddRule(new LuhnValidator());
        Assert.Equal(1, validator.RuleCount);
        validator.ForField(2).AddRule(new LengthValidator());
        Assert.Equal(2, validator.RuleCount);
        validator.ForField(49).AddRule(new CurrencyCodeValidator());
        Assert.Equal(3, validator.RuleCount);
        // Require() does not contribute to RuleCount
        validator.Require(4);
        Assert.Equal(3, validator.RuleCount);
    }

    // ==================== ValidationReport ====================

    [Fact]
    public void ValidationReport_ToString_ProducesDiagnostic()
    {
        var errors = new[]
        {
            ValidationResult.Failure(2, "bad Luhn", "LuhnValidator"),
            ValidationResult.Failure(49, "unknown code", "CurrencyCodeValidator")
        };
        var report = new ValidationReport(errors);
        var text = report.ToString();
        Assert.Contains("2 error(s)", text);
        Assert.Contains("Field 2", text);
        Assert.Contains("Field 49", text);
        Assert.Contains("LuhnValidator", text);
    }

    [Fact]
    public void ValidationReport_Valid_IsValidAndHasNoErrors()
    {
        Assert.True(ValidationReport.Valid.IsValid);
        Assert.Empty(ValidationReport.Valid.Errors);
        Assert.Equal("ValidationReport: OK", ValidationReport.Valid.ToString());
    }

    [Fact]
    public void ValidationReport_ErrorsForField_FiltersByField()
    {
        var errors = new[]
        {
            ValidationResult.Failure(2, "err a"),
            ValidationResult.Failure(2, "err b"),
            ValidationResult.Failure(49, "err c")
        };
        var report = new ValidationReport(errors);
        Assert.Equal(2, report.ErrorsForField(2).Count());
        Assert.Single(report.ErrorsForField(49));
        Assert.Empty(report.ErrorsForField(11));
    }

    [Fact]
    public void ValidationReport_NullErrors_DefaultsToEmpty()
    {
        var report = new ValidationReport(null);
        Assert.True(report.IsValid);
    }

    [Fact]
    public void ValidationResult_SuccessAndFailureFactories_SetFields()
    {
        var s = ValidationResult.Success(5, "MyValidator");
        Assert.True(s.IsValid);
        Assert.Equal(5, s.FieldNumber);
        Assert.Equal("MyValidator", s.ValidatorName);
        Assert.Null(s.ErrorMessage);

        var f = ValidationResult.Failure(6, "bad", "OtherValidator");
        Assert.False(f.IsValid);
        Assert.Equal(6, f.FieldNumber);
        Assert.Equal("bad", f.ErrorMessage);
        Assert.Equal("OtherValidator", f.ValidatorName);
    }

    // ==================== MessageValidationException ====================

    [Fact]
    public void MessageValidationException_AttachesReport()
    {
        var report = new ValidationReport(new[] { ValidationResult.Failure(2, "bad") });
        var ex = new MessageValidationException(report);
        Assert.Same(report, ex.Report);
        Assert.Contains("Message validation failed", ex.Message);
    }

    [Fact]
    public void MessageValidationException_NullReport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MessageValidationException(null));
    }
}
