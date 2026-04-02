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

using System.Text;
using BenchmarkDotNet.Attributes;
using DotNetty.Buffers;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for ASCII length field parsing: zero-alloc byte arithmetic vs old string allocation path.
/// </summary>
[MemoryDiagnoser]
public class FrameDecoderBenchmarks
{
    private IByteBuffer _buffer;

    [Params(2, 4)]
    public int LengthFieldLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _buffer = Unpooled.Buffer(16);
        // Write a sample length field: "0152" or "52" depending on length
        var lengthStr = "152".PadLeft(LengthFieldLength, '0');
        _buffer.WriteBytes(Encoding.ASCII.GetBytes(lengthStr));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Release();
    }

    [Benchmark(Description = "Optimized: GetByte arithmetic (zero-alloc)")]
    public long ParseLength_ByteArithmetic()
    {
        long frameLength = 0;
        for (var i = 0; i < LengthFieldLength; i++)
        {
            var b = _buffer.GetByte(i);
            frameLength = frameLength * 10 + (b - '0');
        }
        return frameLength;
    }

    [Benchmark(Description = "Old: byte[] + ASCII.GetString + long.TryParse")]
    public long ParseLength_StringAlloc()
    {
        var lengthBytes = new byte[LengthFieldLength];
        _buffer.GetBytes(0, lengthBytes);
        var lengthStr = Encoding.ASCII.GetString(lengthBytes);
        long.TryParse(lengthStr, out var frameLength);
        return frameLength;
    }
}
