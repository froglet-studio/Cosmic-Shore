using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// How <see cref="UdpTransport"/>'s datagrams travel (docs/MULTIPLAYER.md §6.7). The transport keeps the
    /// reliability, the connection handshake and the peers; a link only moves whole datagrams to and from
    /// a peer named by an opaque key. <see cref="DirectLink"/> is a plain UDP socket (the key is the peer's
    /// address); <see cref="RelayLink"/> tunnels through a Unity-Relay-protocol server (the key is the
    /// peer's allocation id). Every member is called from the transport's one network thread.
    /// </summary>
    internal interface IDatagramLink : IDisposable
    {
        /// <summary>A server's local port (0 when the link has none, e.g. a relay host).</summary>
        int LocalPort { get; }

        /// <summary>A client's server: the key datagrams to it go to and its answers come from. Null until known (a relay client learns it from ACCEPTED).</summary>
        string ServerKey { get; }

        /// <summary>Why the link cannot work (a refused relay bind, an unknown allocation), or null.</summary>
        string Failure { get; }

        /// <summary>Waits up to <paramref name="microseconds"/> for something to read. False when there is nothing.</summary>
        bool Wait(int microseconds);

        /// <summary>Reads one waiting datagram for the transport into <paramref name="buffer"/>: its length, or -1 when none is left.</summary>
        int Receive(byte[] buffer, out string from);

        /// <summary>Sends one datagram to the peer named <paramref name="to"/>. Failures are silent: the transport resends.</summary>
        void Send(byte[] buffer, int length, string to);

        /// <summary>Once per loop: keepalives, binds, retries of the link's own protocol.</summary>
        void Service(double nowMs);
    }

    /// <summary>The direct link: one UDP socket; a peer's key is its address.</summary>
    internal sealed class DirectLink : IDatagramLink
    {
        readonly Socket _sock;
        readonly IPEndPoint _server;
        readonly Dictionary<string, EndPoint> _addresses = new();

        public int LocalPort { get; }
        public string ServerKey { get; }
        public string Failure => null;

        DirectLink(Socket sock, IPEndPoint server)
        {
            _sock = sock;
            _server = server;
            LocalPort = ((IPEndPoint)sock.LocalEndPoint).Port;
            if (server != null)
            {
                ServerKey = server.ToString();
                _addresses[ServerKey] = server;
            }
        }

        internal static Socket NewSocket(AddressFamily family)
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

        internal static IPAddress Resolve(string address)
        {
            if (IPAddress.TryParse(address, out var ip)) return ip;
            try { return Array.Find(Dns.GetHostAddresses(address), a => a.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.Loopback; }
            catch (SocketException) { return IPAddress.Loopback; }
        }

        public static DirectLink Listen(string address, int port)
        {
            var ip = string.IsNullOrEmpty(address) || address == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(address);
            var s = NewSocket(ip.AddressFamily);
            s.ExclusiveAddressUse = true; // a second instance on the same port must fail and pick another
            try { s.Bind(new IPEndPoint(ip, port)); }
            catch { s.Dispose(); throw; }
            return new DirectLink(s, null);
        }

        public static DirectLink Connect(string address, int port)
        {
            var ip = Resolve(address);
            var s = NewSocket(ip.AddressFamily);
            s.Bind(new IPEndPoint(ip.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
            return new DirectLink(s, new IPEndPoint(ip, port));
        }

        public bool Wait(int microseconds)
        {
            try { return _sock.Poll(microseconds, SelectMode.SelectRead); }
            catch (ObjectDisposedException) { return false; }
        }

        public int Receive(byte[] buffer, out string from)
        {
            from = null;
            while (true)
            {
                EndPoint ep = new IPEndPoint(_sock.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                int n;
                try
                {
                    if (_sock.Available <= 0) return -1;
                    n = _sock.ReceiveFrom(buffer, ref ep);
                }
                catch (SocketException) { continue; } // an ICMP error about an earlier send: not this packet
                catch (ObjectDisposedException) { return -1; }
                if (n <= 0) continue;
                // A loopback server may answer from another loopback address than the one dialled.
                if (_server != null && SameEndPoint(ep, _server)) from = ServerKey;
                else
                {
                    from = ep.ToString();
                    if (!_addresses.ContainsKey(from)) _addresses[from] = ep;
                }
                return n;
            }
        }

        static bool SameEndPoint(EndPoint a, IPEndPoint b)
            => a is IPEndPoint ia && ia.Port == b.Port && (ia.Address.Equals(b.Address) || (IPAddress.IsLoopback(ia.Address) && IPAddress.IsLoopback(b.Address)));

        public void Send(byte[] buffer, int length, string to)
        {
            if (to == null || !_addresses.TryGetValue(to, out var ep)) return;
            try { _sock.SendTo(buffer, 0, length, SocketFlags.None, ep); }
            catch (SocketException) { } // the line is down or the buffer full: the resend timer covers it
            catch (ObjectDisposedException) { }
        }

        public void Service(double nowMs) { }

        /// <summary>Forgets a gone peer's address (the transport calls it when it removes the peer).</summary>
        public void Forget(string key) { if (key != ServerKey) _addresses.Remove(key); }

        public void Dispose() { try { _sock.Dispose(); } catch { } }
    }
}
