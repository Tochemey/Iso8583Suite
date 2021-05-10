using System;
using Iso8583.Common.Netty.Pipelines;

namespace Iso8583.Common
{
  /// <summary>
  ///   ConnectorConfiguration abstract class
  /// </summary>
  public abstract class ConnectorConfiguration
  {
    /// <summary>
    ///   Default read/write idle timeout in seconds (ping interval) = 30 sec.
    /// </summary>
    private const int DefaultIdleTimeout = 30;

    /// <summary>
    ///   Default (max message length) = 8192
    /// </summary>
    private const int DefaultMaxFrameLength = 8192;

    /// <summary>
    ///   Default (length of TCP Frame length) = 2
    /// </summary>
    private const int DefaultFrameLengthFieldLength = 2;

    /// <summary>
    ///   Default (compensation value to add to the value of the length field) = 0
    /// </summary>
    private const int DefaultFrameLengthFieldAdjust = 0;

    /// <summary>
    ///   Default  (the offset of the length field) = 0
    /// </summary>
    private const int DefaultFrameLengthFieldOffset = 0;

    private static readonly int[] DefaultSensitiveDataFields = IsoMessageLoggingHandler.DefaultMaskedFields;

    /// <summary>
    ///   default constructor
    /// </summary>
    protected ConnectorConfiguration()
    {
      EncodeFrameLengthAsString = false;
      AddLoggingHandler = false;
      AddEchoMessageListener = false;
      LogFieldDescription = true;
      LogSensitiveData = true;
      ReplyOnError = false;
      MaxFrameLength = DefaultMaxFrameLength;
      IdleTimeout = DefaultIdleTimeout;
      FrameLenghtFieldLength = DefaultFrameLengthFieldLength;
      FrameLengthFieldOffset = DefaultFrameLengthFieldOffset;
      FrameLengthFieldAdjust = DefaultFrameLengthFieldAdjust;
      SensitiveDataFields = DefaultSensitiveDataFields;
      WorkerThreadCount = Environment.ProcessorCount * 16;
    }

    /// <summary>
    ///   Allows to add default echo message listener to the pipeline
    /// </summary>
    public bool AddEchoMessageListener { get; set; }

    /// <summary>
    ///   Maximum frame length
    /// </summary>
    public int MaxFrameLength { get; set; }

    /// <summary>
    ///   Set channel read/write idle timeout in seconds.
    ///   If no message was received/sent during specified time interval then `Echo` message will be sent
    /// </summary>
    public int IdleTimeout { get; set; }

    /// <summary>
    ///   Returns number of threads in worker.
    ///   Default value is Environment.ProcessorCount * 16
    /// </summary>
    public int WorkerThreadCount { get; set; }

    /// <summary>
    ///   Whether to reply with administrative message in case of message syntax errors. Default value is false.
    /// </summary>
    public bool ReplyOnError { get; set; }

    /// <summary>
    ///   addLoggingHandler `true` if <see cref="IsoMessageLoggingHandler" /> should be added to the pipeline.
    /// </summary>
    public bool AddLoggingHandler { get; set; }

    /// <summary>
    ///   Should log sensitive data (unmasked) or not. Not recommended in production
    ///   `true` if sensitive information like PAN, CVV/CVV2, and Track2 should be printed to log.
    ///   Default value is true
    /// </summary>
    public bool LogSensitiveData { get; set; }

    /// <summary>
    ///   If <code>true</code> then the length header is to be encoded as a String, as opposed to the default binary
    /// </summary>
    public bool EncodeFrameLengthAsString { get; set; }

    /// <summary>
    ///   Array of sensitive fields
    /// </summary>
    public int[] SensitiveDataFields { get; set; }

    /// <summary>
    ///   `true` to print ISO field descriptions in the log
    /// </summary>
    public bool LogFieldDescription { get; set; }

    /// <summary>
    ///   length of TCP frame length field. Default value is 2
    /// </summary>
    public int FrameLenghtFieldLength { get; set; }

    /// <summary>
    ///   The offset of the length field. Default value is 0
    /// </summary>
    public int FrameLengthFieldOffset { get; set; }

    /// <summary>
    ///   Returns the compensation value to add to the value of the length field.
    ///   Default value is 0.
    /// </summary>
    public int FrameLengthFieldAdjust { get; set; }
  }
}