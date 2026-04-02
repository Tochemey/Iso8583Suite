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
using Iso8583.Common.Iso;
using NetCore8583;
using NetCore8583.Parse;

namespace Iso8583.Benchmarks;

/// <summary>
///   Benchmarks for message creation, field population, response creation, and parsing.
/// </summary>
[MemoryDiagnoser]
public class MessageFactoryBenchmarks
{
    private IsoMessageFactory<IsoMessage> _factory;
    private byte[] _serializedMessage;
    private IsoMessage _requestMessage;

    [GlobalSetup]
    public void Setup()
    {
        var mfact = ConfigParser.CreateDefault();
        ConfigParser.ConfigureFromClasspathConfig(mfact, "n8583.xml");
        mfact.UseBinaryMessages = false;
        mfact.Encoding = Encoding.ASCII;
        _factory = new IsoMessageFactory<IsoMessage>(mfact, Iso8583Version.V1987);

        // Pre-create a request for response creation benchmark
        _requestMessage = CreatePopulatedMessage();

        // Pre-serialize for parse benchmark
        var sbytes = _requestMessage.WriteData();
        _serializedMessage = new byte[sbytes.Length];
        Buffer.BlockCopy(sbytes, 0, _serializedMessage, 0, sbytes.Length);
    }

    [Benchmark(Description = "NewMessage + populate 15 fields")]
    public IsoMessage CreateAndPopulate()
    {
        return CreatePopulatedMessage();
    }

    [Benchmark(Description = "CreateResponse from request")]
    public IsoMessage CreateResponse()
    {
        return _factory.CreateResponse(_requestMessage);
    }

    [Benchmark(Description = "ParseMessage from bytes (authorization request)")]
    public IsoMessage ParseMessage()
    {
        return _factory.ParseMessage(_serializedMessage, 0);
    }

    private IsoMessage CreatePopulatedMessage()
    {
        var msg = _factory.NewMessage(0x0200);
        msg.SetField(2, new IsoValue(IsoType.LLVAR, "5164123785712481", 16));
        msg.SetField(3, new IsoValue(IsoType.NUMERIC, "004000", 6));
        msg.SetField(4, new IsoValue(IsoType.NUMERIC, "000000000100", 12));
        msg.SetField(7, new IsoValue(IsoType.DATE10, DateTime.UtcNow));
        msg.SetField(11, new IsoValue(IsoType.ALPHA, "100304", 6));
        msg.SetField(12, new IsoValue(IsoType.DATE12, DateTime.UtcNow));
        msg.SetField(14, new IsoValue(IsoType.DATE_EXP, new DateTime(2027, 2, 1)));
        msg.SetField(19, new IsoValue(IsoType.NUMERIC, "840", 3));
        msg.SetField(22, new IsoValue(IsoType.ALPHA, "A00101A03346", 12));
        msg.SetField(24, new IsoValue(IsoType.NUMERIC, "100", 3));
        msg.SetField(26, new IsoValue(IsoType.NUMERIC, "5814", 4));
        msg.SetField(35, new IsoValue(IsoType.LLVAR, "5164123785712481D17021011408011015360", 37));
        msg.SetField(37, new IsoValue(IsoType.ALPHA, "000000000411", 12));
        msg.SetField(41, new IsoValue(IsoType.ALPHA, "02001101", 8));
        msg.SetField(42, new IsoValue(IsoType.ALPHA, "502101143255555", 15));
        return msg;
    }
}
