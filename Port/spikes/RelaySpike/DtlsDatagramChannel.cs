using System;
using System.Text;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.Security;

namespace Prisma.RelaySpike
{
    /// <summary>
    /// Relay's encrypted mode: DTLS 1.2 with a pre-shared key over UDP to the allocation's "dtls"
    /// endpoint. Per https://docs.unity.com/en-us/relay/dtls-encryption the PSK is the allocation
    /// key (the same 64 bytes used for the BIND HMAC) and the PSK identity ("hint") is the
    /// allocation id's canonical string. Once the handshake is done, the Relay protocol runs
    /// unchanged inside the DTLS records. BouncyCastle (MIT-style license) provides the DTLS stack.
    /// </summary>
    public sealed class DtlsDatagramChannel : IDatagramChannel
    {
        readonly UdpDatagramChannel _udp;
        readonly DtlsTransport _dtls;
        public string Name => "dtls";
        public string NegotiatedCipher { get; }

        /// <summary>
        /// PSK suites offered, strongest first. BouncyCastle's default PskTlsClient list omits the
        /// plain-PSK AEAD suites; Relay answered that default offer with insufficient_security (71).
        /// </summary>
        public static readonly int[] OfferedSuites =
        {
            CipherSuite.TLS_ECDHE_PSK_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_ECDHE_PSK_WITH_CHACHA20_POLY1305_SHA256,
            CipherSuite.TLS_DHE_PSK_WITH_AES_256_GCM_SHA384,
            CipherSuite.TLS_DHE_PSK_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_PSK_WITH_AES_256_GCM_SHA384,
            CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256,
            CipherSuite.TLS_PSK_WITH_CHACHA20_POLY1305_SHA256,
            CipherSuite.TLS_PSK_WITH_AES_128_CCM,
            CipherSuite.TLS_PSK_WITH_AES_128_CCM_8,
            CipherSuite.TLS_ECDHE_PSK_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_PSK_WITH_AES_128_CBC_SHA256,
            CipherSuite.TLS_PSK_WITH_AES_256_CBC_SHA384,
        };

        public DtlsDatagramChannel(string host, int port, Guid allocationId, byte[] key, int handshakeTimeoutMs = 10000, int[]? suites = null)
        {
            _udp = new UdpDatagramChannel(host, port);
            var client = new RelayPskClient(new BcTlsCrypto(new SecureRandom()),
                new BasicTlsPskIdentity(Encoding.ASCII.GetBytes(allocationId.ToString()), key), suites ?? OfferedSuites);
            var protocol = new DtlsClientProtocol();
            var transport = new UdpTransport(_udp, handshakeTimeoutMs);
            _dtls = protocol.Connect(client, transport);
            NegotiatedCipher = client.Cipher;
        }

        public void Send(ReadOnlySpan<byte> datagram) => _dtls.Send(datagram);

        public int Receive(byte[] buffer, int timeoutMs)
        {
            try
            {
                int n = _dtls.Receive(buffer, 0, buffer.Length, timeoutMs);
                return n < 0 ? -1 : n;
            }
            catch (TlsFatalAlert) { return -1; }
            catch (ObjectDisposedException) { return -1; }
        }

        public void Dispose()
        {
            try { _dtls.Close(); } catch (Exception) { /* best effort close_notify */ }
            _udp.Dispose();
        }

        /// <summary>A PSK client offering DTLS 1.2 PSK suites; records which one the server picked.</summary>
        internal sealed class RelayPskClient : PskTlsClient
        {
            public string Cipher { get; private set; } = "?";
            readonly int[] _suites;
            public RelayPskClient(Org.BouncyCastle.Tls.Crypto.TlsCrypto crypto, TlsPskIdentity id, int[] suites) : base(crypto, id) { _suites = suites; }
            protected override ProtocolVersion[] GetSupportedVersions() => ProtocolVersion.DTLSv12.Only();
            protected override int[] GetSupportedCipherSuites()
                => TlsUtilities.GetSupportedCipherSuites(Crypto, _suites);
            public override void NotifySelectedCipherSuite(int selectedCipherSuite)
            {
                base.NotifySelectedCipherSuite(selectedCipherSuite);
                Cipher = CipherSuiteName(selectedCipherSuite);
            }
            public static string CipherSuiteName(int s)
            {
                foreach (var f in typeof(CipherSuite).GetFields())
                    if (f.FieldType == typeof(int) && (int)f.GetValue(null)! == s) return f.Name;
                return "0x" + s.ToString("X4");
            }
        }

        /// <summary>BouncyCastle's DatagramTransport over our UDP channel.</summary>
        sealed class UdpTransport : DatagramTransport
        {
            readonly UdpDatagramChannel _udp;
            readonly int _handshakeTimeoutMs;
            public UdpTransport(UdpDatagramChannel udp, int handshakeTimeoutMs) { _udp = udp; _handshakeTimeoutMs = handshakeTimeoutMs; }
            public int GetReceiveLimit() => 1500 - 28;
            public int GetSendLimit() => 1500 - 28 - 13; // IP+UDP, DTLS record header
            public int Receive(byte[] buf, int off, int len, int waitMillis)
            {
                var tmp = off == 0 && len == buf.Length ? buf : new byte[len];
                int n = _udp.Receive(tmp, waitMillis);
                if (n > 0 && !ReferenceEquals(tmp, buf)) Array.Copy(tmp, 0, buf, off, n);
                return n;
            }
            public int Receive(Span<byte> buffer, int waitMillis)
            {
                var tmp = new byte[buffer.Length];
                int n = _udp.Receive(tmp, waitMillis);
                if (n > 0) tmp.AsSpan(0, n).CopyTo(buffer);
                return n;
            }
            public void Send(byte[] buf, int off, int len) => _udp.Send(buf.AsSpan(off, len));
            public void Send(ReadOnlySpan<byte> buffer) => _udp.Send(buffer);
            public void Close() { }
        }
    }
}
