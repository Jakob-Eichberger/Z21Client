/* Z21 - C#-Implementierung des Protokolls der Kommunikation mit der digitalen
 * Steuerzentrale Z21 oder z21 von Fleischmann/Roco
 * ---------------------------------------------------------------------------
 * Datei:     z21.cs
 * Version:   16.06.2014
 * Besitzer:  Mathias Rentsch (rentsch@online.de)
 * Lizenz:    GPL
 *
 * Die Anwendung und die Quelltextdateien sind freie Software und stehen unter der
 * GNU General Public License. Der Originaltext dieser Lizenz kann eingesehen werden
 * unter http://www.gnu.org/licenses/gpl.html.
 * 
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Z21
{
    public class Z21Communicator(int port) : UdpClient(port), IZ21Communicator
    {
        /// <summary>
        /// Enables or disables Network Address Translation (NAT) traversal on a System.Net.Sockets.UdpClient instance.
        /// </summary>
        /// <param name="allowed">A Boolean value that specifies whether to enable or disable NAT traversal.</param>
        [SupportedOSPlatform("windows")]
        public new void AllowNatTraversal(bool allowed) => base.AllowNatTraversal(allowed);

        /// <summary>
        /// Receives a datagram from a remote host asynchronously.
        /// </summary>
        /// <param name="asyncCallback">An System.AsyncCallback delegate that references the method to invoke when the operation is complete.</param>
        /// <param name="state">A user-defined object that contains information about the receive operation.This object is passed to the requestCallback delegate when the operation is complete.</param>
        public new void BeginReceive(AsyncCallback asyncCallback, object state) => base.BeginReceive(asyncCallback, state);

        /// <summary>
        /// Closes the UDP connection.
        /// </summary>
        public new void Close() => base.Close();

        /// <summary>
        /// Establishes a default remote host using the specified IP address and port number.
        /// </summary>
        /// <param name="address">The System.Net.IPAddress of the remote host to which you intend to send data.</param>
        /// <param name="port">The port number to which you intend send data.</param>
        public new void Connect(IPAddress address, int port) => base.Connect(address, port);

        /// <summary>
        /// Releases the managed and unmanaged resources used by the System.Net.Sockets.UdpClient.
        /// </summary>
        public new void Dispose() => base.Dispose();

        /// <summary>
        /// Ends a pending asynchronous receive.
        /// </summary>
        /// <param name="asyncResult">An System.IAsyncResult object returned by a call to System.Net.Sockets.UdpClient.BeginReceive(System.AsyncCallback,System.Object).</param>
        /// <param name="remoteEP">The specified remote endpoint.</param>
        /// <returns>If successful, an array of bytes that contains datagram data.</returns>
        public new byte[] EndReceive(IAsyncResult asyncResult, ref IPEndPoint? remoteEP) => base.EndReceive(asyncResult, ref remoteEP);

        /// <summary>
        /// Sends a UDP datagram asynchronously to a remote host.
        /// </summary>
        /// <param name="datagram">An array of type System.Byte that specifies the UDP datagram that you intend to send represented as an array of bytes.</param>
        /// <param name="bytes">The number of bytes in the datagram.</param>
        /// <returns>Returns System.Threading.Tasks.Task.</returns>
        public new async Task<int> SendAsync(byte[] datagram, int bytes) => await base.SendAsync(datagram, bytes);
    }
}
