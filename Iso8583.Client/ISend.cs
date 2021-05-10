using System.Threading.Tasks;
using NetCore8583;

namespace Iso8583.Client
{
  /// <summary>
  ///   this interface will be used by the client to send iso message to server
  /// </summary>
  public interface ISend
  {
    /// <summary>
    ///   sends an iso message to the server
    /// </summary>
    /// <param name="message">the iso message to send</param>
    /// <returns></returns>
    Task Send(IsoMessage message);

    /// <summary>
    ///   sends an iso message to the server with a timeout
    /// </summary>
    /// <param name="message">the iso message to send</param>
    /// <param name="timeout">the timeout</param>
    /// <returns></returns>
    Task Send(IsoMessage message, int timeout);
  }
}