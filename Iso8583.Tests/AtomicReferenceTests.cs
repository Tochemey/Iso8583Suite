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

public class AtomicReferenceTests
{
    [Fact]
    public void DefaultConstructor_ValueIsNull()
    {
        var r = new AtomicReference<string>();
        Assert.Null(r.Value);
    }

    [Fact]
    public void Constructor_WithValue_SetsValue()
    {
        var r = new AtomicReference<string>("hello");
        Assert.Equal("hello", r.Value);
    }

    [Fact]
    public void Value_Set_UpdatesValue()
    {
        var r = new AtomicReference<string>("old");
        r.Value = "new";
        Assert.Equal("new", r.Value);
    }

    [Fact]
    public void CompareAndSet_MatchingExpected_SwapsAndReturnsTrue()
    {
        var r = new AtomicReference<string>("initial");
        var result = r.CompareAndSet("initial", "updated");
        Assert.True(result);
        Assert.Equal("updated", r.Value);
    }

    [Fact]
    public void CompareAndSet_NonMatchingExpected_DoesNotSwapAndReturnsFalse()
    {
        var r = new AtomicReference<string>("initial");
        var result = r.CompareAndSet("wrong", "updated");
        Assert.False(result);
        Assert.Equal("initial", r.Value);
    }

    [Fact]
    public void GetAndSet_ReturnsOldAndSetsNew()
    {
        var r = new AtomicReference<string>("old");
        var previous = r.GetAndSet("new");
        Assert.Equal("old", previous);
        Assert.Equal("new", r.Value);
    }

    [Fact]
    public void ImplicitConversion_ToT_ReturnsValue()
    {
        var r = new AtomicReference<string>("test");
        string s = r;
        Assert.Equal("test", s);
    }

    [Fact]
    public void ImplicitConversion_FromT_CreatesReference()
    {
        AtomicReference<string> r = "hello";
        Assert.Equal("hello", r.Value);
    }
}
