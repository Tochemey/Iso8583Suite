namespace Iso8583.Common.Iso
{
    /// <summary>
    ///     Iso 8583 message functions.
    ///     Position three of the MTI specifies the message function which defines how the message should flow within the
    ///     system. Requests are end-to-end messages (e.g., from acquirer to issuer and back with time-outs and automatic
    ///     reversals in place), while advices are point-to-point messages (e.g., from terminal to acquirer, from acquirer to
    ///     network, from network to issuer, with transmission guaranteed over each link, but not necessarily immediately).
    ///     <see cref="https://en.wikipedia.org/wiki/ISO_8583#Message_function" />
    /// </summary>
    public enum MessageFunction
    {
        /// <summary>
        ///     xx0x	Request	Request from acquirer to issuer to carry out an action; issuer may accept or reject
        /// </summary>
        REQUEST = 0x0000,

        /// <summary>
        ///     xx1x	Request response	Issuer response to a request
        /// </summary>
        REQUEST_RESPONSE = 0x0010,

        /// <summary>
        ///     xx2x Advice
        ///     Advice that an action has taken place; receiver can only accept, not reject
        /// </summary>
        ADVICE = 0x0020,

        /// <summary>
        ///     xx3x Advice response
        ///     Response to an advice
        /// </summary>
        ADVICE_RESPONSE = 0x0030,

        /// <summary>
        ///     xx4x	Notification
        ///     Notification that an event has taken place; receiver can only accept, not reject
        /// </summary>
        NOTIFICATION = 0x0040,

        /// <summary>
        ///     xx5x	Notification acknowledgement
        ///     Response to a notification
        /// </summary>
        NOTIFICATION_ACK = 0x0050,

        /// <summary>
        ///     xx6x	Instruction	ISO 8583:2003
        /// </summary>
        INSTRUCTION = 0x0060,

        /// <summary>
        ///     xx7x	Instruction acknowledgement
        /// </summary>
        INSTRUCTION_ACK = 0x0070,

        /// <summary>
        ///     xx8x	Reserved for ISO use
        ///     Some implementations (such as MasterCard) use for positive acknowledgment.[4]
        /// </summary>
        RESERVED_8 = 0x0080,

        /// <summary>
        ///     xx9x	Some implementations (such as MasterCard) use for negative acknowledgement.
        /// </summary>
        RESERVED_9 = 0x0090
    }
}