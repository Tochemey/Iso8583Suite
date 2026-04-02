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

using Iso8583.Common.Iso;
using Xunit;

namespace Iso8583.Tests;

public class MtiTests
{
    [Fact]
    public void Mti_V1987_AuthorizationRequest_Returns0x0100()
    {
        var mti = new MTI(Iso8583Version.V1987, MessageClass.AUTHORIZATION, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);
        // V1987=0x0000, AUTH=0x0100, REQUEST=0x0000, ACQUIRER=0x0000
        Assert.Equal(0x0100, mti.Value());
    }

    [Fact]
    public void Mti_V1987_AuthorizationResponse_Returns0x0110()
    {
        var mti = new MTI(Iso8583Version.V1987, MessageClass.AUTHORIZATION, MessageFunction.REQUEST_RESPONSE,
            MessageOrigin.ACQUIRER);
        Assert.Equal(0x0110, mti.Value());
    }

    [Fact]
    public void Mti_V1987_NetworkManagementRequest_Returns0x0800()
    {
        var mti = new MTI(Iso8583Version.V1987, MessageClass.NETWORK_MANAGEMENT, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);
        Assert.Equal(0x0800, mti.Value());
    }

    [Fact]
    public void Mti_V1987_FinancialAdvice_Returns0x0220()
    {
        var mti = new MTI(Iso8583Version.V1987, MessageClass.FINANCIAL, MessageFunction.ADVICE,
            MessageOrigin.ACQUIRER);
        Assert.Equal(0x0220, mti.Value());
    }

    [Fact]
    public void Mti_V1987_ReversalFromIssuer_Returns0x0402()
    {
        var mti = new MTI(Iso8583Version.V1987, MessageClass.REVERSAL_CHARGEBACK, MessageFunction.REQUEST,
            MessageOrigin.ISSUER);
        Assert.Equal(0x0402, mti.Value());
    }

    [Fact]
    public void Mti_V1993_AuthorizationRequest_Returns0x1100()
    {
        var mti = new MTI(Iso8583Version.V1993, MessageClass.AUTHORIZATION, MessageFunction.REQUEST,
            MessageOrigin.ACQUIRER);
        // V1993=0x1000, AUTH=0x0100
        Assert.Equal(0x1100, mti.Value());
    }

    [Fact]
    public void Mti_CombinesAllComponents()
    {
        var mti = new MTI(Iso8583Version.V2003, MessageClass.FINANCIAL, MessageFunction.ADVICE_RESPONSE,
            MessageOrigin.ISSUER_REPEAT);
        // V2003=0x2000, FINANCIAL=0x0200, ADVICE_RESPONSE=0x0030, ISSUER_REPEAT=0x0003
        Assert.Equal(0x2233, mti.Value());
    }
}
