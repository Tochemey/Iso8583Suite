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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Iso8583.Common.Metrics;
using Microsoft.Extensions.Logging;
using NetCore8583;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Handles iso messages with a chain of <see cref="IIsoMessageListener{T}" />.
  ///   Uses a copy-on-write array for thread-safe listener iteration.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class CompositeIsoMessageHandler<T> : ChannelHandlerAdapter where T : IsoMessage
  {
    private static readonly IIsoMessageListener<T>[] EmptyListeners = Array.Empty<IIsoMessageListener<T>>();

    private readonly bool _failOnError;
    private readonly ILogger _logger;
    private readonly IIso8583Metrics _metrics;
    private readonly object _listenerLock = new();

    /// <summary>
    ///   Copy-on-write snapshot of listeners. Reads are lock-free; writes take the lock.
    /// </summary>
    private volatile IIsoMessageListener<T>[] _listeners = EmptyListeners;

    /// <summary>
    ///   create a new instance of <see cref="CompositeIsoMessageHandler{T}" />
    /// </summary>
    /// <param name="failOnError">When <c>true</c>, exceptions from listeners propagate up the pipeline. When <c>false</c>, they are logged and swallowed.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="metrics">optional metrics provider</param>
    public CompositeIsoMessageHandler(bool failOnError, ILogger logger, IIso8583Metrics metrics = null)
    {
      _failOnError = failOnError;
      _logger = logger;
      _metrics = metrics ?? NullIso8583Metrics.Instance;
    }

    /// <summary>
    ///   default constructor
    /// </summary>
    public CompositeIsoMessageHandler() : this(true, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
    {
    }

    /// <summary>
    ///   This handler is safe to share across channels: listeners are stored in a copy-on-write array,
    ///   and all other state is either readonly or per-invocation (passed via <see cref="IChannelHandlerContext"/>).
    ///   Marking it sharable is required for the server, which installs a single instance on every
    ///   child connection's pipeline.
    /// </summary>
    public override bool IsSharable => true;

    /// <summary>
    ///   Called when a message is read from the channel. Casts to <typeparamref name="T"/> and dispatches
    ///   to the registered listener chain asynchronously.
    /// </summary>
    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
      if (message is not T isoMessage)
      {
        _logger.LogWarning("Received message of type {Type} which is not a supported IsoMessage subclass. Ignoring",
          message.GetType());
        return;
      }

      // Run async handler chain without blocking the event loop.
      // ContinueWith ensures exceptions are propagated to the pipeline instead of being silently lost.
      DoHandleMessageAsync(context, isoMessage).ContinueWith(static (t, state) =>
      {
        var ctx = (IChannelHandlerContext)state!;
        if (t.Exception != null)
          ctx.FireExceptionCaught(t.Exception.InnerException ?? t.Exception);
      }, context, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///   Flushes the channel after all pending reads in the current batch have been processed.
    /// </summary>
    public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

    /// <summary>
    ///   Logs the exception and closes the channel.
    /// </summary>
    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
      _logger.LogError(exception, "Exception caught in channel pipeline");
      context.CloseAsync();
    }

    /// <summary>
    ///   adds a new iso message listener
    /// </summary>
    /// <param name="listener">the message listener</param>
    /// <exception cref="ArgumentNullException">is thrown when the listener is null</exception>
    public void AddListener(IIsoMessageListener<T> listener)
    {
      if (listener == null)
        throw new ArgumentNullException(nameof(listener));

      lock (_listenerLock)
      {
        var current = _listeners;
        var next = new IIsoMessageListener<T>[current.Length + 1];
        Array.Copy(current, next, current.Length);
        next[current.Length] = listener;
        _listeners = next;
      }

      _logger.LogDebug("Adding message listener {Listener}", listener.GetType().Name);
    }

    /// <summary>
    ///   adds a list of iso message listeners
    /// </summary>
    /// <param name="listeners">the list of iso message listeners</param>
    /// <exception cref="ArgumentNullException">thrown when the list of listeners is null or empty</exception>
    public void AddListeners(params IIsoMessageListener<T>[] listeners)
    {
      if (listeners == null || listeners.Length == 0)
        throw new ArgumentNullException(nameof(listeners));

      lock (_listenerLock)
      {
        var current = _listeners;
        var next = new IIsoMessageListener<T>[current.Length + listeners.Length];
        Array.Copy(current, next, current.Length);
        Array.Copy(listeners, 0, next, current.Length, listeners.Length);
        _listeners = next;
      }
    }

    /// <summary>
    ///   removes a given listener
    /// </summary>
    /// <param name="listener">the listener to remove</param>
    public void RemoveListener(IIsoMessageListener<T> listener)
    {
      lock (_listenerLock)
      {
        var current = _listeners;
        var index = Array.IndexOf(current, listener);
        if (index < 0) return;

        if (current.Length == 1)
        {
          _listeners = EmptyListeners;
          return;
        }

        var next = new IIsoMessageListener<T>[current.Length - 1];
        Array.Copy(current, 0, next, 0, index);
        Array.Copy(current, index + 1, next, index, current.Length - index - 1);
        _listeners = next;
      }
    }

    /// <summary>
    ///   handles the iso message received by passing around to the various registered listeners
    /// </summary>
    /// <param name="context">the channel handler context</param>
    /// <param name="isoMessage">the iso message to handle</param>
    private async Task DoHandleMessageAsync(IChannelHandlerContext context, T isoMessage)
    {
      _logger.LogDebug("Handling received message type 0x{Type}", isoMessage.Type.ToString("X4"));

      var sw = Stopwatch.StartNew();

      try
      {
        // Snapshot the listener array (copy-on-write: reads are lock-free)
        var listeners = _listeners;

        for (var i = 0; i < listeners.Length; i++)
        {
          var listener = listeners[i];
          var continueChain = await HandleMessageWithListenerAsync(listener, context, isoMessage);
          if (!continueChain)
          {
            _logger.LogTrace("Stopping further processing of message 0x{Type} after handler {Handler}",
              isoMessage.Type.ToString("X4"), listener.GetType().Name);
            break;
          }
        }

        sw.Stop();
        _metrics.MessageHandled(isoMessage.Type, sw.Elapsed);
      }
      catch (Exception e)
      {
        sw.Stop();
        _metrics.MessageError(isoMessage.Type, e);
        throw;
      }
    }

    /// <summary>
    ///   passes the message handling to the appropriate listener
    /// </summary>
    /// <param name="listener">the message listener</param>
    /// <param name="isoMessage">the iso message</param>
    /// <param name="context">the channel handler context</param>
    /// <returns>true or false</returns>
    private async Task<bool> HandleMessageWithListenerAsync(IIsoMessageListener<T> listener,
      IChannelHandlerContext context, T isoMessage)
    {
      try
      {
        if (!listener.CanHandleMessage(isoMessage))
        {
          _logger.LogTrace("{Listener} cannot handle message 0x{Type}",
            listener.GetType().Name, isoMessage.Type.ToString("X4"));
          return true;
        }

        _logger.LogDebug("Handling IsoMessage[@type=0x{Type}] with {Listener}",
          isoMessage.Type.ToString("x4"),
          listener.GetType().Name);

        return await listener.HandleMessage(context, isoMessage);
      }
      catch (Exception e)
      {
        _logger.LogError(e, "Error in {Listener} handling message 0x{Type}",
          listener.GetType().Name, isoMessage.Type.ToString("X4"));
        if (_failOnError)
          throw;
      }

      return true;
    }
  }
}
