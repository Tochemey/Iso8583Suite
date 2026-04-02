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
using DotNetty.Buffers;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Codecs;
using NetCore8583;
using NetCore8583.Extensions;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for the IsoMessageEncoder comparing the optimized path
///   (BlockCopy + direct ASCII digit writing) vs the old approach
///   (ToString + GetBytes intermediate allocations).
/// </summary>
[MemoryDiagnoser]
public class EncoderBenchmarks
{
    private IsoMessage _message;
    private IByteBuffer _buffer;

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
        _message.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
        _message.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        _message.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        _message.SetField(49, new IsoValue(IsoType.NUMERIC, "840", 3));
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _buffer = Unpooled.Buffer(1024);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _buffer.Release();
    }

    [Benchmark(Description = "Optimized: BlockCopy + direct ASCII digits")]
    public void Encode_Optimized()
    {
        _buffer.Clear();
        var data = SBytesToBytes_BlockCopy(_message.WriteData());
        WriteLengthHeaderAscii(_buffer, data.Length, 4);
        _buffer.WriteBytes(data);
    }

    [Benchmark(Description = "Old: ToString + GetBytes + GetBytes")]
    public void Encode_Old()
    {
        _buffer.Clear();
        var bytea = _message.WriteData();
        var streamToSend = Encoding.ASCII.GetBytes(bytea.ToString(Encoding.ASCII));
        var lengthHeader = Convert.ToString(streamToSend.Length).PadLeft(4, '0');
        _buffer.WriteBytes(lengthHeader.GetBytes());
        _buffer.WriteBytes(streamToSend);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] SBytesToBytes_BlockCopy(sbyte[] source)
    {
        var dest = new byte[source.Length];
        Buffer.BlockCopy(source, 0, dest, 0, source.Length);
        return dest;
    }

    private static void WriteLengthHeaderAscii(IByteBuffer buffer, int length, int headerLength)
    {
        Span<byte> digits = stackalloc byte[headerLength];
        digits.Fill((byte)'0');
        var value = length;
        for (var i = headerLength - 1; i >= 0 && value > 0; i--)
        {
            digits[i] = (byte)('0' + value % 10);
            value /= 10;
        }
        for (var i = 0; i < headerLength; i++)
            buffer.WriteByte(digits[i]);
    }
}
