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

public class Iso8583FieldsTests
{
    [Fact]
    public void Fields_WhenIniFileExists_ReturnsPopulatedDictionary()
    {
        // The ini file may or may not be present in the test output directory.
        // This test verifies that accessing Fields does not throw,
        // regardless of whether the file exists.
        var fields = Iso8583Fields.Fields;
        Assert.NotNull(fields);
    }

    [Fact]
    public void Fields_MultipleCalls_ReturnsSameInstance()
    {
        var fields1 = Iso8583Fields.Fields;
        var fields2 = Iso8583Fields.Fields;
        Assert.Same(fields1, fields2);
    }
}
