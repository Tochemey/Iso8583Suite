namespace Iso8583.Common.Iso
{
  /// <summary>
  ///   Position two of the MTI specifies the overall purpose of the message.
  ///   <see cref="https://en.wikipedia.org/wiki/ISO_8583#Message class" />
  /// </summary>
  public enum MessageClass
  {
    /// <summary>
    ///   x1xx	Authorization message
    ///   Determine if funds are available, get an approval but do not post
    ///   to account for reconciliation. Dual message system (DMS), awaits file exchange
    ///   for posting to the account.
    /// </summary>
    AUTHORIZATION = 0x0100,

    /// <summary>
    ///   x2xx	Financial messages
    ///   Determine if funds are available, get an approval and post directly to the account. Single message system (SMS), no
    ///   file exchange after this.
    /// </summary>
    FINANCIAL = 0x0200,

    /// <summary>
    ///   x3xx	File actions message
    ///   Used for hot-card, TMS and other exchanges
    /// </summary>
    FILE_ACTIONS = 0x0300,

    /// <summary>
    ///   x4xx	Reversal and chargeback messages
    ///   - Reversal (x4x0 or x4x1): Reverses the action of a previous authorization.
    ///   - Chargeback (x4x2 or x4x3): Charges back a previously cleared financial message.
    /// </summary>
    REVERSAL_CHARGEBACK = 0x0400,

    /// <summary>
    ///   x5xx	Reconciliation message
    ///   Transmits settlement information message.
    /// </summary>
    RECONCILIATION = 0x0500,

    /// <summary>
    ///   x6xx	Administrative message
    ///   Transmits administrative advice. Often used for failure messages
    ///   (e.g., message reject or failure to apply).
    /// </summary>
    ADMINISTRATIVE = 0x0600,

    /// <summary>
    ///   x7xx	Fee collection messages
    /// </summary>
    FEE_COLLECTION = 0x0700,

    /// <summary>
    ///   x8xx	Network management message
    ///   Used for secure key exchange, logon, echo test and other network functions.
    /// </summary>
    NETWORK_MANAGEMENT = 0x0800
  }
}