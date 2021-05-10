namespace Iso8583.Common.Iso
{
  public interface IMessageFactory<T>
  {
    /// <summary>
    ///   creates a new iso message given a type
    /// </summary>
    /// <param name="type">the message type</param>
    /// <returns>the new message</returns>
    T NewMessage(int type);

    /// <summary>
    ///   creates a new iso message given the message class, message function and the message origin
    /// </summary>
    /// <param name="messageClass">the message class</param>
    /// <param name="messageFunction">the message function</param>
    /// <param name="messageOrigin">the message origin</param>
    /// <returns>the new message</returns>
    T NewMessage(MessageClass messageClass, MessageFunction messageFunction, MessageOrigin messageOrigin);

    /// <summary>
    ///   creates a response corresponding a request
    /// </summary>
    /// <param name="request">the request</param>
    /// <returns>the response</returns>
    T CreateResponse(T request);

    /// <summary>
    ///   creates a response corresponding to a request. If copyAllFields is set to true all fields in the request
    ///   will be copied to the response.
    /// </summary>
    /// <param name="request">the request</param>
    /// <param name="copyAllFields">
    ///   If true, copies all fields from the request to the response, overwriting any values
    ///   already
    /// </param>
    /// <returns>the response</returns>
    T CreateResponse(T request, bool copyAllFields);

    /// <summary>
    ///   Parses a byte buffer containing an ISO8583 message. The buffer must
    ///   not include the length header. If it includes the ISO message header, then its length must be specified so the
    ///   message type can be found.
    /// </summary>
    /// <param name="buf"> The byte buffer containing the message</param>
    /// <param name="isoHeaderLength">
    ///   Specifies the position at which the message  type is located, which is also the length
    ///   of the ISO header.
    /// </param>
    /// <param name="binaryIsoHeader"></param>
    /// <returns></returns>
    T ParseMessage(byte[] buf, int isoHeaderLength, bool binaryIsoHeader = false);
  }
}