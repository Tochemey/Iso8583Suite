namespace Iso8583.Common.Iso
{
  /// <summary>
  ///   Message type indicator
  /// </summary>
  public class MTI
  {
    private readonly Iso8583Version _isoVersion;
    private readonly MessageClass _messageClass;
    private readonly MessageFunction _messageFunction;
    private readonly MessageOrigin _messageOrigin;

    /// <summary>
    ///   creates a new instance of MTI
    /// </summary>
    /// <param name="isoVersion"></param>
    /// <param name="messageClass"></param>
    /// <param name="messageFunction"></param>
    /// <param name="messageOrigin"></param>
    public MTI(Iso8583Version isoVersion, MessageClass messageClass, MessageFunction messageFunction,
      MessageOrigin messageOrigin)
    {
      _isoVersion = isoVersion;
      _messageClass = messageClass;
      _messageFunction = messageFunction;
      _messageOrigin = messageOrigin;
    }

    /// <summary>
    ///   returns the MTI value
    /// </summary>
    /// <returns></returns>
    public int Value() => (int) _isoVersion + (int) _messageClass + (int) _messageFunction + (int) _messageOrigin;
  }
}