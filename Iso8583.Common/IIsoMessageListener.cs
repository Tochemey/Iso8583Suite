using DotNetty.Transport.Channels;
using NetCore8583;

namespace Iso8583.Common
{
    /// <summary>
    /// This interface will be implemented by the various handlers in the pipeline
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IIsoMessageListener<in T> where T : IsoMessage
    {
        /// <summary>
        ///  return true when a given message can be handled by <see cref="HandleMessage"/>
        /// </summary>
        /// <param name="isoMessage">the iso message to check. The message must not be null</param>
        /// <returns>true or false</returns>
        bool CanHandleMessage(T isoMessage);

        /// <summary>
        /// handles the received message and returns false when the message should not be handled by another handler
        /// or true on the contrary
        /// </summary>
        /// <param name="context">the current channel handler context</param>
        /// <param name="isoMessage">the iso message to handle</param>
        /// <returns>true f message should be handled by subsequent message listeners</returns>
        bool HandleMessage(IChannelHandlerContext context, T isoMessage);
    }
}