using System;
using System.Collections.Generic;
using System.Linq;
using DotNetty.Transport.Channels;
using Microsoft.Extensions.Logging;
using NetCore8583;

namespace Iso8583.Common.Netty.Pipelines
{
  /// <summary>
  ///   Handles iso messages with a chain of <see cref="IIsoMessageListener{T}" />
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class CompositeIsoMessageHandler<T> : ChannelHandlerAdapter where T : IsoMessage
  {
    private readonly bool _failOnError;
    private readonly ILogger<CompositeIsoMessageHandler<T>> _logger;
    private readonly IList<IIsoMessageListener<T>> _messageListeners;

    /// <summary>
    ///   create a new instance of <see cref="CompositeIsoMessageHandler{T}" />
    /// </summary>
    /// <param name="failOnError"></param>
    /// <param name="logger"></param>
    public CompositeIsoMessageHandler(bool failOnError, ILogger<CompositeIsoMessageHandler<T>> logger)
    {
      _failOnError = failOnError;
      _logger = logger;
      _messageListeners = new List<IIsoMessageListener<T>>();
    }

    /// <summary>
    ///   default constructor
    /// </summary>
    public CompositeIsoMessageHandler() : this(true,
      new LoggerFactory().CreateLogger<CompositeIsoMessageHandler<T>>())
    {
      // TODO: check the logger
    }

    public override void ChannelRead(IChannelHandlerContext context, object message)
    {
      T isoMessage;
      try
      {
        // here we are doing some casting that may fail
        isoMessage = message as T;
      }
      catch (Exception)
      {
        _logger.LogError("IsoMessage subclass {Sublclass} is not supported by {Class}. Doing nothing",
          message.GetType(),
          GetType());
        return;
      }

      DoHandleMessage(context, isoMessage);
      base.ChannelRead(context, message);
    }

    /// <summary>
    ///   adds a new iso message listener
    /// </summary>
    /// <param name="listener">the message listener</param>
    /// <exception cref="ArgumentNullException">is thrown when the listener is null</exception>
    public void AddListener(IIsoMessageListener<T> listener)
    {
      // Let us make sure that listener is not null
      if (listener != null) _messageListeners.Add(listener);
      else throw new ArgumentNullException(nameof(listener));
    }

    /// <summary>
    ///   adds a list if iso message listeners
    /// </summary>
    /// <param name="listeners">the list of iso message listeners</param>
    /// <exception cref="ArgumentNullException">thrown when the list of listeners is null or empty</exception>
    public void AddListeners(params IIsoMessageListener<T>[] listeners)
    {
      if (listeners != null && listeners.Any())
        foreach (var listener in listeners)
          _messageListeners.Add(listener);
      else throw new ArgumentNullException(nameof(listeners));
    }

    /// <summary>
    ///   removes a given listener
    /// </summary>
    /// <param name="listener">the listener to remove</param>
    public void RemoveListener(IIsoMessageListener<T> listener)
    {
      _messageListeners.Remove(listener);
    }

    /// <summary>
    ///   handles the iso message received by passing around to the various registered listeners
    /// </summary>
    /// <param name="context">the channel handler context</param>
    /// <param name="isoMessage">the iso message to handle</param>
    private void DoHandleMessage(IChannelHandlerContext context, T isoMessage)
    {
      var nextListener = true;
      var size = _messageListeners.Count;
      var i = 0;
      while (nextListener && i < size)
      {
        var listener = _messageListeners[i];
        nextListener = HandleMessageWithListener(listener, context, isoMessage);
        if (nextListener == false)
          _logger.LogTrace("Stopping further processing of message {Message} after handler {Handler}",
            isoMessage, listener);
        i++;
      }
    }

    /// <summary>
    ///   passes the message handling to the appropriate listener
    /// </summary>
    /// <param name="listener">the message listener</param>
    /// <param name="isoMessage">the iso message</param>
    /// <param name="context">the channel handler context</param>
    /// <returns>true or false</returns>
    private bool HandleMessageWithListener(IIsoMessageListener<T> listener, IChannelHandlerContext context,
      T isoMessage)
    {
      try
      {
        if (listener.CanHandleMessage(isoMessage))
        {
          _logger.LogDebug("Handling IsoMessage[@type=0x{Type}] with {Listener}",
            isoMessage.Type.ToString("x4"),
            listener);
          return listener.HandleMessage(context, isoMessage);
        }
      }
      catch (Exception e)
      {
        _logger.LogDebug("cannot evaluate {Listener}.CanHandle({Arg}) due to {Err}", listener,
          isoMessage.GetType(), e.Message);
        if (_failOnError)
          throw;
      }

      return true;
    }
  }
}