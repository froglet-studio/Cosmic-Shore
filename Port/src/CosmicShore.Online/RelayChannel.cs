using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace CosmicShore.Online
{
    /// <summary>
    /// What carries Relay protocol datagrams to one Relay server: plain UDP, or DTLS over UDP. The Relay
    /// protocol is identical on both; DTLS only wraps it. Used from one thread at a time.
    /// </summary>
    internal interface IRelayChannel : IDisposable
    {
        string Name { get; }
        void Send(ReadOnlySpan<byte> datagram);
        /// <summary>One datagram, waiting up to <paramref name="waitMicros"/> (0 = only one already here); -1 if none.</summary>
        int Receive(byte[] buffer, int waitMicros);
    }

    /// <summary>A failure the link cannot recover from (a DTLS alert, a closed socket).</summary>
    internal sealed class RelayChannelException : Exception
    {
        public RelayChannelException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>Unencrypted UDP to the allocation's "udp" endpoint.</summary>
    internal sealed class UdpRelayChannel : IRelayChannel
    {
        readonly Socket _socket;
        public string Name => "udp";
        public IPEndPoint Remote { get; }

        public UdpRelayChannel(string host, int port)
        {
            var ip = IPAddress.TryParse(host, out var a) ? a : Array.Find(Dns.GetHostAddresses(host), x => x.AddressFamily == AddressFamily.InterNetwork) ?? Dns.GetHostAddresses(host)[0];
            Remote = new IPEndPoint(ip, port);
            _socket = new Socket(ip.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            try { _socket.ReceiveBufferSize = 4 * 1024 * 1024; _socket.SendBufferSize = 4 * 1024 * 1024; } catch (SocketException) { }
            if (OperatingSystem.IsWindows())
                try { _socket.IOControl(unchecked((int)0x9800000C), new byte[] { 0 }, null); } catch (SocketException) { } // SIO_UDP_CONNRESET off
            _socket.Connect(Remote); // fixes the 5-tuple: the Relay server ties our address to the allocation
        }

        internal Socket Socket => _socket;

        public void Send(ReadOnlySpan<byte> datagram)
        {
            try { _socket.Send(datagram); }
            catch (SocketException) { } // a full buffer or a moment offline: the transport's resends cover it
            catch (ObjectDisposedException) { }
        }

        public int Receive(byte[] buffer, int waitMicros)
        {
            try
            {
                if (waitMicros > 0 ? !_socket.Poll(waitMicros, SelectMode.SelectRead) : _socket.Available <= 0) return -1;
                return _socket.Receive(buffer);
            }
            catch (SocketException) { return -1; } // ICMP port unreachable and friends: a lost datagram
            catch (ObjectDisposedException) { return -1; }
        }

        public void Dispose() => _socket.Dispose();
    }

    /// <summary>
    /// Relay's encrypted mode: DTLS 1.2 with a pre-shared key over UDP to the allocation's "dtls" endpoint.
    /// Per https://docs.unity.com/en-us/relay/dtls-encryption the PSK is the allocation key (the 64 bytes that
    /// also sign the BIND) and the PSK identity is the allocation id's canonical string. Relay accepts exactly
    /// one suite, TLS_PSK_WITH_AES_128_GCM_SHA256 (measured 2026-10-08: every other PSK suite gets alert 71,
    /// insufficient_security), so that is the only one offered. BouncyCastle provides the DTLS stack.
    /// The handshake blocks (one to three round trips); the link runs it on the transport's own thread.
    /// </summary>
    internal sealed class DtlsRelayChannel : IRelayChannel
    {
        public static readonly int[] Suites = { CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256 };
        /// <summary>DTLS 1.2 record overhead with AES-128-GCM: 13 header + 8 explicit nonce + 16 tag.</summary>
        public const int Overhead = 37;

        readonly UdpRelayChannel _udp;
        readonly DtlsTransport _dtls;
        public string Name => "dtls";

        public DtlsRelayChannel(string host, int port, Guid allocationId, byte[] key, int handshakeTimeoutMs = 10000)
        {
            _udp = new UdpRelayChannel(host, port);
            try
            {
                var client = new RelayPskClient(new BcTlsCrypto(new SecureRandom()),
                    new BasicTlsPskIdentity(Encoding.ASCII.GetBytes(allocationId.ToString()), key), handshakeTimeoutMs);
                _dtls = new DtlsClientProtocol().Connect(client, new Adapter(_udp));
            }
            catch (Exception e)
            {
                _udp.Dispose();
                throw new RelayChannelException($"DTLS handshake with {host}:{port} failed: {e.Message}", e);
            }
        }

        public void Send(ReadOnlySpan<byte> datagram)
        {
            try { _dtls.Send(datagram); }
            catch (TlsFatalAlert e) { throw new RelayChannelException("DTLS send failed: " + e.Message, e); }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        public int Receive(byte[] buffer, int waitMicros)
        {
            // BouncyCastle treats a 0 ms wait as "forever", so a drain checks the socket first; a record queued
            // behind another in one datagram is read on the next pass (a 1 ms wait), never lost.
            if (waitMicros <= 0 && _udp.Socket.Available <= 0) return -1;
            try
            {
                int n = _dtls.Receive(buffer, 0, buffer.Length, Math.Max(1, waitMicros / 1000));
                return n < 0 ? -1 : n;
            }
            catch (TlsFatalAlert e) { throw new RelayChannelException("DTLS alert from the Relay server: " + e.Message, e); }
            catch (TlsTimeoutException) { return -1; }
            catch (ObjectDisposedException) { return -1; }
        }

        public void Dispose()
        {
            try { _dtls.Close(); } catch (Exception) { } // best-effort close_notify
            _udp.Dispose();
        }

        /// <summary>A DTLS 1.2 PSK client offering only the suite Relay accepts.</summary>
        sealed class RelayPskClient : PskTlsClient
        {
            readonly int _handshakeTimeoutMs;
            public RelayPskClient(Org.BouncyCastle.Tls.Crypto.TlsCrypto crypto, TlsPskIdentity id, int handshakeTimeoutMs) : base(crypto, id) { _handshakeTimeoutMs = handshakeTimeoutMs; }
            public override int GetHandshakeTimeoutMillis() => _handshakeTimeoutMs;
            protected override ProtocolVersion[] GetSupportedVersions() => ProtocolVersion.DTLSv12.Only();
            protected override int[] GetSupportedCipherSuites() => TlsUtilities.GetSupportedCipherSuites(Crypto, Suites);
        }

        /// <summary>BouncyCastle's DatagramTransport over the UDP channel.</summary>
        sealed class Adapter : DatagramTransport
        {
            readonly UdpRelayChannel _udp;
            readonly byte[] _tmp = new byte[2048];
            public Adapter(UdpRelayChannel udp) { _udp = udp; }
            public int GetReceiveLimit() => 1500 - 28;
            public int GetSendLimit() => 1500 - 28 - 13; // IP + UDP headers, DTLS record header
            public int Receive(byte[] buf, int off, int len, int waitMillis) => Receive(buf.AsSpan(off, len), waitMillis);
            public int Receive(Span<byte> buffer, int waitMillis)
            {
                int n = _udp.Receive(_tmp, Math.Max(1, waitMillis) * 1000);
                if (n <= 0) return -1;
                n = Math.Min(n, buffer.Length);
                _tmp.AsSpan(0, n).CopyTo(buffer);
                return n;
            }
            public void Send(byte[] buf, int off, int len) => _udp.Send(buf.AsSpan(off, len));
            public void Send(ReadOnlySpan<byte> buffer) => _udp.Send(buffer);
            public void Close() { }
        }
    }
}
