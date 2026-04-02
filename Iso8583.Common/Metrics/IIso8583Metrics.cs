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
  ///   Interface for observability hooks in the ISO 8583 pipeline.
  ///   Implement this to integrate with Prometheus, OpenTelemetry, Application Insights, etc.
  /// </summary>
  public interface IIso8583Metrics
  {
    /// <summary>
    ///   Called when a message is encoded and sent to the wire.
    /// </summary>
    /// <param name="mti">the message type indicator</param>
    void MessageSent(int mti);

    /// <summary>
    ///   Called when a message is received and decoded from the wire.
    /// </summary>
    /// <param name="mti">the message type indicator</param>
    void MessageReceived(int mti);

    /// <summary>
    ///   Called after a message handler completes processing.
    /// </summary>
    /// <param name="mti">the message type indicator</param>
    /// <param name="duration">time spent handling the message</param>
    void MessageHandled(int mti, TimeSpan duration);

    /// <summary>
    ///   Called when an error occurs during message processing.
    /// </summary>
    /// <param name="mti">the message type indicator (0 if unknown)</param>
    /// <param name="exception">the exception that occurred</param>
    void MessageError(int mti, Exception exception);

    /// <summary>
    ///   Called when a new connection is established (client connected or server accepted a connection).
    /// </summary>
    void ConnectionEstablished();

    /// <summary>
    ///   Called when a connection is lost or closed.
    /// </summary>
    void ConnectionLost();
  }
}
