using System;
using System.Net;
using System.Net.Sockets;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Whatever carries <see cref="UdpTransport"/>'s packets: a plain UDP socket
    /// (<see cref="SocketDatagramLink"/>), or an internet relay that wraps each packet for a relay
    /// server to forward (CosmicShore.Online's Unity Relay link). The reliability, ordering,
    /// fragmentation, keepalive and timeout logic sits above this seam, so every link gets the same
    /// guarantees (docs/MULTIPLAYER.md §6.6, docs/RELAY.md).
    ///
    /// Threading: <see cref="UdpTransport"/>'s network thread is the only caller, so an
    /// implementation needs no locks of its own.
    /// </summary>
    internal interface IDatagramLink : IDisposable
    {
        /// <summary>
        /// One datagram into <paramref name="buffer"/>; waits up to <paramref name="waitMicros"/> for it
        /// (0 = only what has already arrived). Returns its length, or -1 when nothing came.
        /// <paramref name="from"/> identifies the sender: equal senders give endpoints whose
        /// <see cref="object.ToString"/> is equal, which is how the transport keys its peers.
        /// </summary>
        int Receive(byte[] buffer, int waitMicros, out EndPoint from);

        /// <summary>Sends one datagram. A send that cannot go out is dropped: the resend timer covers it.</summary>
        void Send(byte[] buffer, int length, EndPoint to);

        /// <summary>Housekeeping once per network-thread pass (a relay's binding and keepalive pings).</summary>
        void Service(double nowMs);

        /// <summary>True when <paramref name="from"/> is the server a client asked to reach at <paramref name="server"/>.</summary>
        bool IsServer(EndPoint from, EndPoint server);

        /// <summary>The local port a server listens on (0 when the link has no port of its own, e.g. a relay).</summary>
        int LocalPort { get; }

        /// <summary>Set once the link can no longer carry anything (the relay refused us, the line is gone); the transport then closes.</summary>
        string Fault { get; }
    }

    /// <summary>The plain UDP socket link: what <see cref="UdpTransport"/> has always used.</summary>
    internal sealed class SocketDatagramLink : IDatagramLink
    {
        readonly Socket _sock;

        public SocketDatagramLink(Socket sock) { _sock = sock; }

        public int LocalPort => _sock.LocalEndPoint is IPEndPoint ip ? ip.Port : 0;
        public string Fault => null;

        public static Socket NewSocket(AddressFamily family)
        {
            var s = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
            // A burst (a scene snapshot is hundreds of fragments) must not overflow the kernel's buffer.
            try { s.ReceiveBufferSize = 4 * 1024 * 1024; s.SendBufferSize = 4 * 1024 * 1024; } catch (SocketException) { }
            if (OperatingSystem.IsWindows())
            {
                // Windows reports an ICMP "port unreachable" from an earlier send as an error on the next
                // receive (SIO_UDP_CONNRESET); a server must not lose its socket to one gone client.
                try { s.IOControl(unchecked((int)0x9800000C), new byte[] { 0 }, null); } catch (SocketException) { }
            }
            return s;
        }

        public int Receive(byte[] buffer, int waitMicros, out EndPoint from)
        {
            from = new IPEndPoint(_sock.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    if (waitMicros > 0 ? !_sock.Poll(waitMicros, SelectMode.SelectRead) : _sock.Available <= 0) return -1;
                    return _sock.ReceiveFrom(buffer, ref from);
                }
                catch (SocketException) { waitMicros = 0; } // an ICMP error about an earlier send: not this packet
                catch (ObjectDisposedException) { return -1; }
            }
        }

        public void Send(byte[] buffer, int length, EndPoint to)
        {
            try { _sock.SendTo(buffer, 0, length, SocketFlags.None, to); }
            catch (SocketException) { } // the line is down or the buffer full: the resend timer covers it
            catch (ObjectDisposedException) { }
        }

        public void Service(double nowMs) { }

        public bool IsServer(EndPoint from, EndPoint server)
            => from is IPEndPoint ia && server is IPEndPoint b && ia.Port == b.Port
               && (ia.Address.Equals(b.Address) || (IPAddress.IsLoopback(ia.Address) && IPAddress.IsLoopback(b.Address)));

        public void Dispose() { try { _sock.Dispose(); } catch { } }
    }
}
