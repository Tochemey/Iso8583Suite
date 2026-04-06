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

using System.Runtime.InteropServices;
using Xunit;

namespace Iso8583.Tests;

/// <summary>
///   xUnit <see cref="FactAttribute"/> variant that marks the test as skipped when the
///   host OS is macOS or Linux. Used to exclude DotNetty TLS integration tests where the
///   DotNetty <c>TlsHandler</c> is known to hang during the handshake, while still running
///   them on Windows CI.
/// </summary>
public sealed class FactSkipOnMacOSAttribute : FactAttribute
{
    public FactSkipOnMacOSAttribute(string reason = "Skipped on macOS/Linux")
    {
        Timeout = 30_000;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Skip = reason;
    }
}
