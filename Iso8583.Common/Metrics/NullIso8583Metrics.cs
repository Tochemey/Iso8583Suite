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

namespace Iso8583.Common.Metrics
{
  /// <summary>
  ///   No-op metrics implementation. Used when no metrics provider is configured.
  /// </summary>
  public sealed class NullIso8583Metrics : IIso8583Metrics
  {
    public static readonly NullIso8583Metrics Instance = new();

    public void MessageSent(int mti) { }
    public void MessageReceived(int mti) { }
    public void MessageHandled(int mti, TimeSpan duration) { }
    public void MessageError(int mti, Exception exception) { }
    public void ConnectionEstablished() { }
    public void ConnectionLost() { }
  }
}
