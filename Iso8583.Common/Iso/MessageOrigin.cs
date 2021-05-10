namespace Iso8583.Common.Iso
{
  /// <summary>
  ///   Position four of the MTI defines the location of the message source within the payment chain.
  ///   <see cref="https://en.wikipedia.org/wiki/ISO_8583#Message_origin" />
  /// </summary>
  public enum MessageOrigin
  {
    /// <summary>
    ///   xxx0	Acquirer
    /// </summary>
    ACQUIRER = 0x0000,

    /// <summary>
    ///   xxx1	Acquirer repeat
    /// </summary>
    ACQUIRER_REPEAT = 0x0001,

    /// <summary>
    ///   xxx2	Issuer
    /// </summary>
    ISSUER = 0x0002,

    /// <summary>
    ///   xxx3	Issuer repeat
    /// </summary>
    ISSUER_REPEAT = 0x0003,

    /// <summary>
    ///   xxx4	Other
    /// </summary>
    OTHER = 0x0004,

    /// <summary>
    ///   xxx5	Other repeat
    /// </summary>
    OTHER_REPEAT = 0x0005
  }
}