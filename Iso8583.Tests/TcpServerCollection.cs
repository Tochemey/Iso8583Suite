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

namespace Iso8583.Tests;

/// <summary>
///   Test collection for tests that spin up real Iso8583Server instances with full event loop groups.
///   Running these sequentially prevents thread/resource exhaustion under parallel test execution.
/// </summary>
[CollectionDefinition(nameof(TcpServerCollection), DisableParallelization = true)]
public class TcpServerCollection;
