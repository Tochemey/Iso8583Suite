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
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   listens to the iso Echo message and handle it
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class EchoMessageListener<T> : IIsoMessageListener<T> where T : IsoMessage
  {
    private readonly IMessageFactory<T> _messageFactory;

    /// <summary>
    ///   creates a new instance of the EchoMessageListener
    /// </summary>
    /// <param name="messageFactory">The message factory used to create echo response messages.</param>
    public EchoMessageListener(IMessageFactory<T> messageFactory) => _messageFactory = messageFactory;

    /// <inheritdoc />
    public bool CanHandleMessage(T isoMessage) =>
      isoMessage is { Type: (int)MessageClass.NETWORK_MANAGEMENT };

    /// <summary>
    ///   sends EchoResponse message. Always returns <code>false</code>.
    /// </summary>
    /// <param name="context">the channel handler context</param>
    /// <param name="isoMessage">the message to handle</param>
    /// <returns><code>false</code> - message should not be handled by any other handler</returns>
    public async Task<bool> HandleMessage(IChannelHandlerContext context, T isoMessage)
    {
      var response = _messageFactory.CreateResponse(isoMessage);
      await context.WriteAndFlushAsync(response);
      return false;
    }
  }
}
