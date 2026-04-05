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

namespace Iso8583.Common
{
  /// <summary>
  ///   Defines a strategy for configuring the DotNetty channel pipeline.
  ///   Implementations add codecs, handlers, and other channel handlers to the pipeline
  ///   based on the provided <typeparamref name="T"/> configuration.
  /// </summary>
  /// <typeparam name="T">The connector configuration type.</typeparam>
  public interface IPipelineConfigurator<in T> where T : ConnectorConfiguration
  {
    /// <summary>
    ///   Configures the pipeline.
    /// </summary>
    /// <param name="pipeline">the channel pipeline</param>
    /// <param name="configuration">the configuration</param>
    void ConfigurePipeline(IChannelPipeline pipeline,
      T configuration);
  }
}