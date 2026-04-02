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

using DotNetty.Transport.Bootstrapping;
using Iso8583.Common;
using Iso8583.Common.Iso;
using Iso8583.Common.Netty.Pipelines;
using NetCore8583;

namespace Iso8583.Server
{
  public abstract class ServerConnector<T, TC> : Iso8583Connector<T, TC>
    where T : IsoMessage
    where TC : ConnectorConfiguration
  {
    /// <summary>
    ///   the server bootstrap. <see cref="ServerBootstrap" />
    /// </summary>
    protected ServerBootstrap Bootstrap;

    /// <summary>
    ///   creates a new instance of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageHandler">the message handler</param>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ServerConnector(CompositeIsoMessageHandler<T> messageHandler,
      IMessageFactory<T> messageFactory,
      TC configuration) : base(messageHandler, messageFactory, configuration)
    {
    }

    /// <summary>
    ///   auxiliary constructor to create a new of Iso8583ServerConnector
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ServerConnector(IMessageFactory<T> messageFactory,
      TC configuration) : base(messageFactory, configuration)
    {
    }


    /// <summary>
    ///   the connector configurator
    /// </summary>
    protected IServerConnectorConfigurator<TC> ConnectorConfigurator { get; set; }

    protected abstract ServerBootstrap CreateBootstrap();
    
    protected ServerBootstrap GetBootstrap() => Bootstrap;

    /// <summary>
    ///   configures the server bootstrap
    /// </summary>
    /// <param name="bootstrap">the server bootstrap</param>
    protected void ConfigureBootstrap(ServerBootstrap bootstrap)
    {
      ConnectorConfigurator?.ConfigureBootstrap(bootstrap,
        Configuration);
    }
  }
}