using NetCore8583;
using NetCore8583.Parse;
using NetCore8583.Util;

namespace Iso8583.Common.Iso
{
  public class IsoMessageFactory<T> : IMessageFactory<T> where T : IsoMessage
  {
    private readonly Iso8583Version _isoVersion;
    private readonly MessageFactory<T> _messageFactory;

    /// <summary>
    ///   creates a new instance of IsoMessageFactory given the message factory and the Iso 8583 Version
    /// </summary>
    /// <param name="messageFactory">the message factory</param>
    /// <param name="isoVersion">the iso 8583 version</param>
    public IsoMessageFactory(MessageFactory<T> messageFactory, Iso8583Version isoVersion)
    {
      _messageFactory = messageFactory;
      _isoVersion = isoVersion;
    }

    /// <summary>
    ///   creates a new instance of IsoMessageFactory given the Iso 8583 Version
    /// </summary>
    /// <param name="isoVersion">the iso 8583 version</param>
    public IsoMessageFactory(Iso8583Version isoVersion) : this(DefaultMessageFactory(), isoVersion)
    {
    }

    /// <inheritdoc />
    public T NewMessage(int type) => _messageFactory.NewMessage(type);

    /// <inheritdoc />
    public T NewMessage(MessageClass messageClass, MessageFunction messageFunction, MessageOrigin messageOrigin)
    {
      var mti = new MTI(_isoVersion, messageClass, messageFunction, messageOrigin);
      return _messageFactory.NewMessage(mti.Value());
    }

    /// <inheritdoc />
    public T CreateResponse(T request) => _messageFactory.CreateResponse(request);

    /// <inheritdoc />
    public T CreateResponse(T request, bool copyAllFields) => _messageFactory.CreateResponse(request, copyAllFields);

    /// <inheritdoc />
    public T ParseMessage(byte[] buf, int isoHeaderLength, bool binaryIsoHeader = false)
    {
      var bytea = buf.ToInt8();
      return _messageFactory.ParseMessage(bytea, isoHeaderLength, binaryIsoHeader);
    }

    /// <summary>
    ///   convenient method to create a default message factory
    /// </summary>
    /// <returns>the created message factory</returns>
    private static MessageFactory<T> DefaultMessageFactory() => ConfigParser.CreateDefault() as MessageFactory<T>;
  }
}