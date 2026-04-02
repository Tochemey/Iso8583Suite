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

using Iso8583.Common;
using Xunit;

namespace Iso8583.Tests;

public class SslConfigurationTests
{
    [Fact]
    public void DefaultValues_AllFalseOrNull()
    {
        var ssl = new SslConfiguration();
        Assert.False(ssl.Enabled);
        Assert.False(ssl.MutualTls);
        Assert.Null(ssl.CertificatePath);
        Assert.Null(ssl.CertificatePassword);
        Assert.Null(ssl.CaCertificatePath);
        Assert.Null(ssl.TargetHost);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var ssl = new SslConfiguration
        {
            Enabled = true,
            CertificatePath = "/path/to/cert.pfx",
            CertificatePassword = "password",
            MutualTls = true,
            CaCertificatePath = "/path/to/ca.pem",
            TargetHost = "payment.example.com"
        };

        Assert.True(ssl.Enabled);
        Assert.Equal("/path/to/cert.pfx", ssl.CertificatePath);
        Assert.Equal("password", ssl.CertificatePassword);
        Assert.True(ssl.MutualTls);
        Assert.Equal("/path/to/ca.pem", ssl.CaCertificatePath);
        Assert.Equal("payment.example.com", ssl.TargetHost);
    }
}
