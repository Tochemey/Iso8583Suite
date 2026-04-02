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
using Iso8583.Common;

namespace Iso8583.Client
{
  /// <summary>
  ///   Client configuration
  /// </summary>
  public class ClientConfiguration : ConnectorConfiguration
  {
    /// <summary>
    ///   create a new instance of ClientConfiguration
    /// </summary>
    /// <param name="reconnectInterval">reconnect base interval in milliseconds</param>
    public ClientConfiguration(int reconnectInterval) => ReconnectInterval = reconnectInterval;

    /// <summary>
    ///   default constructor
    /// </summary>
    public ClientConfiguration()
    {
      ReconnectInterval = 100;
      MaxReconnectDelay = 30000;
      MaxReconnectAttempts = 10;
      AutoReconnect = true;
    }

    /// <summary>
    ///   Base reconnect interval in milliseconds. Used as the initial delay before exponential backoff.
    /// </summary>
    public int ReconnectInterval { get; set; }

    /// <summary>
    ///   Maximum delay in milliseconds between reconnection attempts.
    /// </summary>
    public int MaxReconnectDelay { get; set; }

    /// <summary>
    ///   Maximum number of reconnection attempts. Set to 0 for unlimited.
    /// </summary>
    public int MaxReconnectAttempts { get; set; }

    /// <summary>
    ///   Whether to automatically reconnect on connection loss. Default is true.
    /// </summary>
    public bool AutoReconnect { get; set; }

    /// <inheritdoc />
    public override void Validate()
    {
      base.Validate();
      if (ReconnectInterval <= 0)
        throw new ArgumentException($"{nameof(ReconnectInterval)} must be > 0, got {ReconnectInterval}");
      if (MaxReconnectDelay <= 0)
        throw new ArgumentException($"{nameof(MaxReconnectDelay)} must be > 0, got {MaxReconnectDelay}");
      if (MaxReconnectAttempts < 0)
        throw new ArgumentException($"{nameof(MaxReconnectAttempts)} must be >= 0, got {MaxReconnectAttempts}");
    }
  }
}
