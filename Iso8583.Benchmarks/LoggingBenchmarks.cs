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
using System.Text;
using BenchmarkDotNet.Attributes;
using DotNetty.Handlers.Logging;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for IsoMessageLoggingHandler message formatting with PAN masking.
/// </summary>
[MemoryDiagnoser]
public class LoggingBenchmarks
{
    private IsoMessage _message;
    private BenchmarkableLoggingHandler _handlerWithSensitive;
    private BenchmarkableLoggingHandler _handlerMasked;

    [GlobalSetup]
    public void Setup()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;

        _message = mfact.NewMessage(0x0200);
        _message.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        _message.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        _message.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        _message.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        _message.SetField(35, new IsoValue(IsoType.LLVAR, "5164123785712481D17021011408011015360", 37));
        _message.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
        _message.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        _message.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        _message.SetField(45, new IsoValue(IsoType.LLVAR, "B5164123785712481^SUPPLIED/NOT^17021011408011015360", 51));
        _message.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));

        _handlerWithSensitive = new BenchmarkableLoggingHandler(printSensitiveData: true);
        _handlerMasked = new BenchmarkableLoggingHandler(printSensitiveData: false);
    }

    [Benchmark(Description = "Format message with sensitive data visible")]
    public string Format_WithSensitiveData()
    {
        return _handlerWithSensitive.FormatMessage(_message);
    }

    [Benchmark(Description = "Format message with PAN/track masking")]
    public string Format_WithMasking()
    {
        return _handlerMasked.FormatMessage(_message);
    }

    /// <summary>
    ///   Subclass to expose the protected Format method for benchmarking.
    /// </summary>
    internal class BenchmarkableLoggingHandler : IsoMessageLoggingHandler
    {
        public BenchmarkableLoggingHandler(bool printSensitiveData)
            : base(LogLevel.DEBUG, printSensitiveData, true)
        {
        }

        public string FormatMessage(IsoMessage message)
        {
            return Format(null, "WRITE", message);
        }
    }
}
