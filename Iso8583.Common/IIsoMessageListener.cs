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

using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using NetCore8583;

namespace Iso8583.Common
{
  /// <summary>
  ///   This interface will be implemented by the various handlers in the pipeline
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public interface IIsoMessageListener<in T> where T : IsoMessage
  {
    /// <summary>
    ///   return true when a given message can be handled by <see cref="HandleMessage" />
    /// </summary>
    /// <param name="isoMessage">the iso message to check. The message must not be null</param>
    /// <returns>true or false</returns>
    bool CanHandleMessage(T isoMessage);

    /// <summary>
    ///   handles the received message and returns false when the message should not be handled by another handler
    ///   or true on the contrary
    /// </summary>
    /// <param name="context">the current channel handler context</param>
    /// <param name="isoMessage">the iso message to handle</param>
    /// <returns>true if message should be handled by subsequent message listeners</returns>
    Task<bool> HandleMessage(IChannelHandlerContext context, T isoMessage);
  }
}
