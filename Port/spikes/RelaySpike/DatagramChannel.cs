using System;
using System.Net;
using System.Net.Sockets;

namespace Prisma.RelaySpike
{
    /// <summary>
    /// Whatever carries Relay datagrams to one Relay server: plain UDP, or DTLS over UDP.
    /// The Relay protocol is identical on both; DTLS only wraps it.
    /// </summary>
    public interface IDatagramChannel : IDisposable
    {
        string Name { get; }
        void Send(ReadOnlySpan<byte> datagram);
        /// <summary>Bytes received into <paramref name="buffer"/>, or -1 if nothing arrived within the timeout.</summary>
        int Receive(byte[] buffer, int timeoutMs);
    }

    /// <summary>Unencrypted UDP to the allocation's "udp" endpoint.</summary>
    public sealed class UdpDatagramChannel : IDatagramChannel
    {
        readonly Socket _socket;
        public string Name => "udp";
        public IPEndPoint Remote { get; }
        public IPEndPoint Local => (IPEndPoint)_socket.LocalEndPoint!;

        public UdpDatagramChannel(string host, int port)
        {
            var ip = IPAddress.TryParse(host, out var a) ? a : Dns.GetHostAddresses(host)[0];
            Remote = new IPEndPoint(ip, port);
            _socket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Connect(Remote); // fixes the 5-tuple; the server binds our IP:port to the allocation
        }

        public void Send(ReadOnlySpan<byte> datagram) => _socket.Send(datagram);

        public int Receive(byte[] buffer, int timeoutMs)
        {
            try
            {
                if (!_socket.Poll(timeoutMs * 1000, SelectMode.SelectRead)) return -1;
                return _socket.Receive(buffer);
            }
            catch (SocketException) { return -1; } // ICMP port unreachable etc.: treat as a drop, like UDP
            catch (ObjectDisposedException) { return -1; }
        }

        internal Socket Socket => _socket;
        public void Dispose() => _socket.Dispose();
    }
}
