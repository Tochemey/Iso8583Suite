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
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Extensions;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for message decoding comparing Unsafe.As reinterpret vs ToInt8 copy.
/// </summary>
[MemoryDiagnoser]
public class DecoderBenchmarks
{
    private byte[] _messageBytes;
    private MessageFactory<IsoMessage> _messageFactory;

    [GlobalSetup]
    public void Setup()
    {
        _messageFactory = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(_messageFactory, "n8583.xml");
        _messageFactory.UseBinaryMessages = false;
        _messageFactory.Encoding = Encoding.ASCII;

        // Create a realistic message and serialize it
        var msg = _messageFactory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        msg.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        msg.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        msg.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));

        var sbytes = msg.WriteData();
        _messageBytes = new byte[sbytes.Length];
        Buffer.BlockCopy(sbytes, 0, _messageBytes, 0, sbytes.Length);
    }

    [Benchmark(Description = "Optimized: Unsafe.As reinterpret (zero-copy)")]
    public IsoMessage Decode_UnsafeAs()
    {
        var bytes = _messageBytes;
        var sbytes = Unsafe.As<byte[], sbyte[]>(ref bytes);
        return _messageFactory.ParseMessage(sbytes, 0);
    }

    [Benchmark(Description = "Old: ToInt8 copy")]
    public IsoMessage Decode_ToInt8Copy()
    {
        var sbytes = _messageBytes.ToInt8();
        return _messageFactory.ParseMessage(sbytes, 0);
    }
}
