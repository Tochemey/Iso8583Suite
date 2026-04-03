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

using DotNetty.Transport.Channels;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Common
{
  public abstract class Iso8583Connector<T, TC>
    where T : IsoMessage
    where TC : ConnectorConfiguration
  {
    private IChannel _channel;

    /// <summary>
    ///   Creates a new instance of <see cref="Iso8583Connector{T, TC}"/>.
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected Iso8583Connector(IMessageFactory<T> messageFactory,
      TC configuration)
    {
      configuration.Validate();
      MessageHandler = new CompositeIsoMessageHandler<T>();
      MessageFactory = messageFactory;
      Configuration = configuration;
      if (configuration.AddEchoMessageListener)
        MessageHandler.AddListener(new EchoMessageListener<T>(messageFactory));
    }

    /// <summary>
    ///   the message handler
    /// </summary>
    protected CompositeIsoMessageHandler<T> MessageHandler { get; }

    /// <summary>
    ///   the message factory
    /// </summary>
    protected IMessageFactory<T> MessageFactory { get; }

    /// <summary>
    ///   the server configuration
    /// </summary>
    protected TC Configuration { get; }


    /// <summary>
    ///   the boss event loop group. <see cref="MultithreadEventLoopGroup" />
    /// </summary>
    protected MultithreadEventLoopGroup BossEventLoopGroup { get; private  set; } 

    /// <summary>
    ///   the worker thread event loop group. <see cref="MultithreadEventLoopGroup" />
    /// </summary>
    protected MultithreadEventLoopGroup WorkerEventLoopGroup { get; private set; }

    /// <summary>
    ///   creates the boss event loop group (acceptor threads for server)
    /// </summary>
    protected void CreateBossEventLoopGroup()
    {
      BossEventLoopGroup = new MultithreadEventLoopGroup();
    }

    /// <summary>
    ///   creates the worker event loop group (I/O threads)
    /// </summary>
    protected void CreateWorkerEventLoopGroup()
    {
      WorkerEventLoopGroup = new MultithreadEventLoopGroup(Configuration.WorkerThreadCount);
    } 

    /// <summary>
    ///   adds a iso message handler
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void AddMessageListener(IIsoMessageListener<T> handler) => MessageHandler.AddListener(handler);

    /// <summary>
    ///   removes an iso message handler
    /// </summary>
    /// <param name="handler">the iso message handler</param>
    public void RemoveMessageListener(IIsoMessageListener<T> handler) => MessageHandler.RemoveListener(handler);


    /// <summary>
    ///   sets the network channel
    /// </summary>
    /// <param name="channel">the channel</param>
    protected void SetChannel(IChannel channel)
    {
      _channel = channel;
    }

    /// <summary>
    ///   gets the network channel
    /// </summary>
    /// <returns></returns>
    protected IChannel GetChannel() => _channel;

    /// <summary>
    ///   checks whether the channel has started or not
    /// </summary>
    /// <returns>true when the server has started and false otherwise</returns>
    public bool IsStarted()
    {
      var channel = GetChannel();
      return channel is { IsOpen: true };
    }

    /// <summary>
    ///   checks whether the channel is active or not
    /// </summary>
    /// <returns>true when the channel is inactive or not</returns>
    protected bool IsChannelInactive()
    {
      var channel = GetChannel();
      return channel is not { IsActive: true };
    }
  }
}