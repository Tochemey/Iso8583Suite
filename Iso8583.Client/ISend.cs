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
using System.Threading;
using System.Threading.Tasks;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   this interface will be used by the client to send iso message to server
  /// </summary>
  public interface ISend
  {
    /// <summary>
    ///   sends an iso message to the server (fire-and-forget)
    /// </summary>
    /// <param name="message">the iso message to send</param>
    Task Send(IsoMessage message);

    /// <summary>
    ///   sends an iso message to the server with a send timeout
    /// </summary>
    /// <param name="message">the iso message to send</param>
    /// <param name="timeout">the send timeout in milliseconds</param>
    Task Send(IsoMessage message, int timeout);

    /// <summary>
    ///   Sends a request and waits for the correlated response.
    ///   Correlation is based on STAN (field 11) and message type.
    /// </summary>
    /// <param name="message">the request message (must contain field 11/STAN)</param>
    /// <param name="timeout">how long to wait for the response</param>
    /// <param name="cancellationToken">optional cancellation token</param>
    /// <returns>the correlated response message</returns>
    Task<IsoMessage> SendAndReceive(IsoMessage message, TimeSpan timeout,
      CancellationToken cancellationToken = default);
  }
}
