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
using DotNetty.Transport.Channels;
using Iso8583.Common;
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   Base class for ISO 8583 TCP clients. Sets up the SpanNetty <see cref="Bootstrap"/>
  ///   with TCP options (nodelay, keepalive, reuse-addr) and delegates further customization
  ///   to an optional <see cref="IClientConnectorConfigurator{T}"/>.
  /// </summary>
  /// <typeparam name="T">The ISO message type.</typeparam>
  /// <typeparam name="TC">The client configuration type.</typeparam>
  public abstract class ClientConnector<T, TC> : Iso8583Connector<T, TC>
    where T : IsoMessage
    where TC : ConnectorConfiguration
  {
    /// <summary>
    ///   the client bootstrap. <see cref="Bootstrap" />
    /// </summary>
    protected Bootstrap Bootstrap;

    /// <summary>
    ///   Creates a new instance of <see cref="ClientConnector{T, TC}"/>.
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="configuration">the configuration</param>
    protected ClientConnector(IMessageFactory<T> messageFactory,
      TC configuration) : base(messageFactory, configuration)
    {
    }

    /// <summary>
    ///   the connector configurator
    /// </summary>
    protected IClientConnectorConfigurator<TC> ConnectorConfigurator { get; set; }
    

    /// <summary>
    ///   Returns the configured <see cref="Bootstrap"/> instance.
    /// </summary>
    protected Bootstrap GetBootstrap() => Bootstrap;
    
    /// <summary>
    ///   configures the client bootstrap
    /// </summary>
    /// <param name="bootstrap">the client bootstrap</param>
    protected void ConfigureBootstrap(Bootstrap bootstrap)
    {
      bootstrap
        .Option(ChannelOption.TcpNodelay, true)
        .Option(ChannelOption.AutoRead, true)
        .Option(ChannelOption.SoKeepalive, true)
        .Option(ChannelOption.SoReuseaddr, true);

      ConnectorConfigurator?.ConfigureBootstrap(bootstrap,
        Configuration);
    }
  }
}