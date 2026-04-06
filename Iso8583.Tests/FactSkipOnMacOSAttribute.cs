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

using Xunit;

namespace Iso8583.Tests;

/// <summary>
///   xUnit <see cref="FactAttribute"/> variant that unconditionally skips the test.
///   DotNetty's <c>TlsHandler</c> hangs during the TLS handshake on all CI platforms
///   (macOS, Linux, and Windows). The <see cref="TlsMutualAuthTests.Diagnostic_RawSslStream_HandshakeSucceedsWithSelfSignedCert"/>
///   test validates TLS correctness using raw <c>SslStream</c> without DotNetty.
/// </summary>
public sealed class FactSkipOnMacOSAttribute : FactAttribute
{
    public FactSkipOnMacOSAttribute(string reason = "DotNetty TlsHandler hangs during handshake on all platforms")
    {
        Skip = reason;
    }
}
