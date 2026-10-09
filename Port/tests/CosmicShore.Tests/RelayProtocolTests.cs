using System;
using System.Linq;
using System.Security.Cryptography;
using CosmicShore.Online;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Relay message encoder/decoder against the byte layouts the live Relay servers accepted in the
    /// 2026-10-08 spike (docs/RELAY.md): big-endian, [DA 72 00 type] header, a 295-byte BIND signed with
    /// HMAC-SHA256 under the allocation key, 38 bytes of RELAY overhead, ids in RFC 4122 byte order.
    /// </summary>
    public class RelayProtocolTests
    {
        static byte[] Seq(int n, int start = 0) => Enumerable.Range(start, n).Select(i => (byte)i).ToArray();

        // A real allocation id from a live run, as the service printed it and as allocationIdBytes carried it.
        const string LiveId = "96313be5-f5af-4ed3-b127-d0bf3c095742";
        static readonly byte[] LiveIdBytes = { 0x96, 0x31, 0x3b, 0xe5, 0xf5, 0xaf, 0x4e, 0xd3, 0xb1, 0x27, 0xd0, 0xbf, 0x3c, 0x09, 0x57, 0x42 };

        [Fact]
        public void AllocationIdBytes_AreTheGuidInRfc4122Order()
        {
            Assert.Equal(Guid.Parse(LiveId), new Guid(LiveIdBytes, bigEndian: true));
            Assert.Equal(LiveId, new RelayPeer(LiveIdBytes).Id.ToString());
            Assert.Equal("relay:" + LiveId, new RelayPeer(LiveIdBytes).ToString());
        }

        [Fact]
        public void Bind_IsHeaderAcceptModeNonceLengthDataThenHmacOverAllOfIt()
        {
            var connData = Seq(255, 1);   // a real allocation's connectionData is 255 bytes
            var key = Seq(64, 100);       // and its key 64
            var buf = new byte[400];
            int n = RelayProtocol.WriteBind(buf, 0x1234, connData, key);

            Assert.Equal(295, n);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x00 }, buf[..4]); // signature, version 0, BIND
            Assert.Equal(0, buf[4]);                                         // AcceptMode AUTO
            Assert.Equal(new byte[] { 0x12, 0x34 }, buf[5..7]);              // nonce, big-endian
            Assert.Equal(255, buf[7]);
            Assert.Equal(connData, buf[8..263]);
            Assert.Equal(HMACSHA256.HashData(key, buf.AsSpan(0, 263)), buf[263..295]);
        }

        [Fact]
        public void Ping_ConnectRequest_Close_Disconnect_Layouts()
        {
            var buf = new byte[400];
            int n = RelayProtocol.WritePing(buf, LiveIdBytes, 0xBEEF);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x02 }.Concat(LiveIdBytes).Concat(new byte[] { 0xBE, 0xEF }).ToArray(), buf[..n]);

            var hostConn = Seq(50, 7); // /join's hostConnectionData was 50 bytes
            n = RelayProtocol.WriteConnectRequest(buf, LiveIdBytes, hostConn);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x03 }.Concat(LiveIdBytes).Append((byte)50).Concat(hostConn).ToArray(), buf[..n]);

            n = RelayProtocol.WriteClose(buf, LiveIdBytes);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x0B }.Concat(LiveIdBytes).ToArray(), buf[..n]);

            var other = Seq(16, 200);
            n = RelayProtocol.WriteDisconnect(buf, LiveIdBytes, other);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x09 }.Concat(LiveIdBytes).Concat(other).ToArray(), buf[..n]);
        }

        [Fact]
        public void Relay_Has38BytesOverhead_AndABigEndianLength()
        {
            var to = Seq(16, 50);
            var content = Seq(300);
            var buf = new byte[2048];
            int n = RelayProtocol.WriteRelay(buf, LiveIdBytes, to, content);
            Assert.Equal(38 + 300, n);
            Assert.Equal(38, RelayProtocol.RelayOverhead);
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x0A }, buf[..4]);
            Assert.Equal(LiveIdBytes, buf[4..20]);
            Assert.Equal(to, buf[20..36]);
            Assert.Equal(new byte[] { 0x01, 0x2C }, buf[36..38]);
            Assert.Equal(content, buf[38..n]);

            Assert.True(RelayProtocol.TryParse(buf.AsSpan(0, n), out var m));
            Assert.Equal(RelayMessageType.Relay, m.Type);
            Assert.Equal(LiveIdBytes, m.FromAllocationId.ToArray());
            Assert.Equal(to, m.ToAllocationId.ToArray());
            Assert.Equal(content, m.Payload.ToArray());
        }

        [Fact]
        public void Relay_RefusesContentPastTheMeasuredCap()
        {
            var buf = new byte[4096];
            Assert.Equal(1394, RelayProtocol.MaxRelayContent);
            RelayProtocol.WriteRelay(buf, LiveIdBytes, LiveIdBytes, new byte[1394]);
            Assert.Throws<ArgumentException>(() => RelayProtocol.WriteRelay(buf, LiveIdBytes, LiveIdBytes, new byte[1395]));
            // Prisma's largest packet fits with room for DTLS's 37 bytes as well.
            Assert.True(CosmicShore.Engine.Networking.UdpTransport.MaxPacket + RelayProtocol.RelayOverhead + DtlsRelayChannel.Overhead < 1300);
        }

        [Fact]
        public void Parse_ServerMessages()
        {
            Assert.True(RelayProtocol.TryParse(new byte[] { 0xDA, 0x72, 0x00, 0x01 }, out var bound));
            Assert.Equal(RelayMessageType.BindReceived, bound.Type);

            var host = Seq(16, 1);
            var accepted = new byte[] { 0xDA, 0x72, 0x00, 0x06 }.Concat(host).Concat(LiveIdBytes).ToArray();
            Assert.True(RelayProtocol.TryParse(accepted, out var a));
            Assert.Equal(RelayMessageType.Accepted, a.Type);
            Assert.Equal(host, a.FromAllocationId.ToArray()); // From = the host
            Assert.Equal(LiveIdBytes, a.ToAllocationId.ToArray()); // To = the joiner

            var echo = new byte[] { 0xDA, 0x72, 0x00, 0x02 }.Concat(LiveIdBytes).Concat(new byte[] { 0x00, 0x07 }).ToArray();
            Assert.True(RelayProtocol.TryParse(echo, out var ping));
            Assert.Equal(7, ping.PingNumber);

            var error = new byte[] { 0xDA, 0x72, 0x00, 0x0C }.Concat(LiveIdBytes).Append((byte)3).ToArray();
            Assert.True(RelayProtocol.TryParse(error, out var e));
            Assert.Equal(RelayErrorCode.ClientPlayerMismatch, e.Error);

            var close = new byte[] { 0xDA, 0x72, 0x00, 0x0B }.Concat(LiveIdBytes).ToArray();
            Assert.True(RelayProtocol.TryParse(close, out var c));
            Assert.Equal(RelayMessageType.Close, c.Type);
        }

        [Theory]
        [InlineData(new byte[] { 0xDA, 0x73, 0x00, 0x01 })]           // wrong signature
        [InlineData(new byte[] { 0xDA, 0x72, 0x00 })]                 // truncated header
        [InlineData(new byte[] { 0xDA, 0x72, 0x00, 0x06, 1, 2, 3 })]  // ACCEPTED without its ids
        [InlineData(new byte[] { 0xDA, 0x72, 0x00, 0x63 })]           // unknown type
        public void Parse_RejectsMalformed(byte[] datagram) => Assert.False(RelayProtocol.TryParse(datagram, out _));

        [Fact]
        public void Parse_RejectsARelayShorterThanItsLength()
        {
            var buf = new byte[100];
            int n = RelayProtocol.WriteRelay(buf, LiveIdBytes, LiveIdBytes, new byte[40]);
            Assert.False(RelayProtocol.TryParse(buf.AsSpan(0, n - 1), out _));
        }

        [Fact]
        public void QosRequest_FollowsThePublishedLayout_AndAResponseEchoesItsCustomData()
        {
            var buf = new byte[64];
            int n = RelayRegions.WriteQosRequest(buf, 3, 0xABCD, 0x0102030405060708);
            Assert.Equal(0x59, buf[0]);
            Assert.Equal(0x00, buf[1]);
            int titleLen = buf[2];          // includes the length byte itself
            Assert.Equal(n, 2 + titleLen + 1 + 2 + 8);
            Assert.True(n >= 15);           // the protocol's minimum request
            // The server's answer: magic 0x95, flow, then the custom data (title removed).
            var resp = new byte[] { 0x95, 0x00 }.Concat(buf[(2 + titleLen)..n]).ToArray();
            Assert.True(RelayRegions.TryReadQosResponse(resp, 0xABCD, out byte seq, out byte flow));
            Assert.Equal(3, seq);
            Assert.Equal(0, flow);
            Assert.False(RelayRegions.TryReadQosResponse(resp, 0x1111, out _, out _)); // someone else's
        }

        [Theory]
        [InlineData("1.2.3.4:7778", "1.2.3.4", 7778)]
        [InlineData("[2001:db8::1]:9999", "2001:db8::1", 9999)]
        [InlineData("qos.example.com:7000", "qos.example.com", 7000)]
        public void QosEndpoints_Split(string endpoint, string host, int port)
            => Assert.Equal((host, port), RelayRegions.Split(endpoint));
    }
}
