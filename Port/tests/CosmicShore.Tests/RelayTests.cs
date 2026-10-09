using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The relay layer (docs/MULTIPLAYER.md §6.7): Unity Relay's wire format byte for byte (the BIND
    /// HMAC checked against an independent implementation, Python's hmac), Froglet's relay server
    /// enforcing what the service enforces, the REST shape, and Froglet's UDP transport running
    /// through it. The transport contract checks also run over the relay (NetTransportContractTests).
    /// </summary>
    public class RelayTests : IDisposable
    {
        readonly FrogletRelayServer server = FrogletRelayServer.Start();

        public void Dispose() => server.Dispose();

        // ── The wire format ─────────────────────────────────────────

        [Fact]
        public void Bind_IsLaidOutAsTheServiceExpects_AndSignedWithHmacSha256()
        {
            var b = new byte[RelayProtocol.BindLength];
            var conn = Enumerable.Range(0, 68).Select(i => (byte)i).ToArray(); // shorter than 255: zero-padded
            var key = Enumerable.Repeat((byte)0x11, 32).ToArray();
            Assert.Equal(295, RelayProtocol.WriteBind(b, 0x0102, conn, key));
            Assert.Equal(new byte[] { 0xDA, 0x72, 0x00, 0x00 }, b[..4]);   // signature, version 0, BIND
            Assert.Equal(0, b[4]);                                          // accept mode AUTO
            Assert.Equal(new byte[] { 0x02, 0x01 }, b[5..7]);              // nonce, little-endian
            Assert.Equal(255, b[7]);                                        // connection data is always 255 bytes
            Assert.Equal(conn, b[8..76]);
            Assert.All(b[76..263], x => Assert.Equal(0, x));
            // Golden value from Python: hmac.new(key, msg[:263], hashlib.sha256)
            Assert.Equal("ffb25d9d15e0eff36bf42efb2ba5602f44619ebb6329dc533353d7630279cb50", Convert.ToHexString(b[263..295]).ToLowerInvariant());
        }

        [Fact]
        public void Relay_CarriesItsLengthBigEndian_AndTheOtherMessagesHaveTheirSizes()
        {
            var from = Enumerable.Repeat((byte)1, 16).ToArray();
            var to = Enumerable.Repeat((byte)2, 16).ToArray();
            var b = new byte[2048];
            Assert.Equal(38 + 300, RelayProtocol.WriteRelay(b, from, to, new byte[300]));
            Assert.Equal((byte)RelayProtocol.Relay, b[3]);
            Assert.Equal(new byte[] { 0x01, 0x2C }, b[36..38]);
            Assert.Equal(300, RelayProtocol.RelayContentLength(b));
            Assert.Throws<ArgumentException>(() => RelayProtocol.WriteRelay(b, from, to, new byte[1401]));
            Assert.Equal(22, RelayProtocol.WritePing(b, from, 7));
            Assert.Equal(276, RelayProtocol.WriteConnectRequest(b, from, new byte[10]));
            Assert.Equal(36, RelayProtocol.WriteAccepted(b, from, to));
            Assert.Equal(20, RelayProtocol.WriteClose(b, from));
            Assert.Equal(21, RelayProtocol.WriteError(b, from, RelayProtocol.ErrNotConnected));
            Assert.Equal(RelayProtocol.ErrNotConnected, b[20]);
        }

        [Fact]
        public void AnAllocation_ParsesTheServicesOwnExample()
        {
            // The Relay Allocations API's documented join response, verbatim.
            var json = System.Text.Json.Nodes.JsonNode.Parse("""
                { "meta": { "requestId": "0d72c1b8-75fd-11eb-b76c-6750be13f34a", "status": 200 }, "data": { "allocation": { "region": "us-central1", "allocationId": "f26ed6cc-6ba7-11eb-b7e1-4f0436c8d458", "allocationIdBytes": "8m7WzGunEeu34U8ENsjUWA==", "relayServer": { "ipV4": "192.168.0.1", "port": 5678 }, "key": "8DC79RXblN3/vVzltbUvaIN4ImPNVK2xQKpsc6Peo2w=", "connectionData": "fPcLLaF90f7HiOcskwHioPJajjmw1noQ3KfvOp692yleIBxHtVHnJqpjMqlIoXUE7rWFe51echUTe8cF5caM2HZuoJaf1A==", "hostConnectionData": "MVVl79nviNYSdgFL7ctIO/vefOoVQA/+Ec7UoQL6aApB80yVMVPdRafSmbQ434P0YItWn6z5LfM9HHx9fD8G9dnnZ2Yjzg==", "serverEndpoints": [ { "connectionType": "udp", "host": "192.168.0.1", "port": 5678, "network": "udp", "secure": false, "reliable": false } ] } }}
                """);
            var a = RelayAllocation.FromJson(json);
            Assert.Equal("192.168.0.1", a.ServerHost);
            Assert.Equal(5678, a.ServerPort);
            Assert.True(a.IsJoin);
            Assert.Equal(16, a.AllocationIdBytes.Length);
            // allocationIdBytes is the UUID in network order: the same bytes as the id string, big-endian.
            Assert.Equal(Guid.Parse("f26ed6cc-6ba7-11eb-b7e1-4f0436c8d458").ToByteArray(bigEndian: true), a.AllocationIdBytes);
        }

        // ── The server enforces what the service enforces ───────────

        sealed class RawClient : IDisposable
        {
            readonly UdpClient _udp = new(0);
            readonly IPEndPoint _relay;
            public RawClient(FrogletRelayServer s) { _relay = new IPEndPoint(IPAddress.Loopback, s.UdpPort); _udp.Client.ReceiveTimeout = 600; }
            public void Send(byte[] b, int n) => _udp.Send(b, n, _relay);
            public byte[] Receive() { try { IPEndPoint ep = null; return _udp.Receive(ref ep); } catch (SocketException) { return null; } }
            public void Dispose() => _udp.Dispose();
        }

        [Fact]
        public void ABindWithTheWrongKey_IsIgnored_AndTheRightOneIsAcknowledged()
        {
            var a = server.Allocate(3);
            using var c = new RawClient(server);
            var b = new byte[RelayProtocol.BindLength];
            RelayProtocol.WriteBind(b, 0, a.ConnectionData, new byte[64]); // wrong key
            c.Send(b, b.Length);
            Assert.Null(c.Receive());
            RelayProtocol.WriteBind(b, 0, a.ConnectionData, a.Key);
            c.Send(b, b.Length);
            var reply = c.Receive();
            Assert.True(RelayProtocol.TryReadHeader(reply, out var type));
            Assert.Equal(RelayProtocol.BindReceived, type);
            Assert.Equal(1, server.BindsRejected);
        }

        [Fact]
        public void APing_IsEchoed_AndARelayToAStranger_IsRefused()
        {
            var a = server.Allocate(3);
            var stranger = server.Allocate(3);
            using var c = new RawClient(server);
            var b = new byte[2048];
            c.Send(b, RelayProtocol.WriteBind(b, 0, a.ConnectionData, a.Key));
            c.Receive();
            int n = RelayProtocol.WritePing(b, a.AllocationIdBytes, 42);
            c.Send(b, n);
            Assert.Equal(b[..n], c.Receive());
            c.Send(b, RelayProtocol.WriteRelay(b, a.AllocationIdBytes, stranger.AllocationIdBytes, new byte[] { 1 }));
            var err = c.Receive();
            Assert.True(RelayProtocol.TryReadHeader(err, out var type));
            Assert.Equal(RelayProtocol.Error, type);
            Assert.Equal(RelayProtocol.ErrNotConnected, err[20]);
        }

        [Fact]
        public async Task TheRestApi_AllocatesJoinCodesAndJoins_InTheServicesShape()
        {
            var client = new RelayAllocationClient(server.BaseUrl);
            var host = await client.AllocateAsync(3);
            Assert.Equal(server.UdpPort, host.ServerPort);
            Assert.Equal(RelayProtocol.KeyLength, host.Key.Length);
            var code = await client.CreateJoinCodeAsync(host);
            Assert.Matches("^[6789BCDFGHJKLMNPQRTW]{6}$", code);
            Assert.Equal(code, await client.CreateJoinCodeAsync(host)); // idempotent
            var join = await client.JoinAsync(code.ToLowerInvariant());     // case-insensitive
            Assert.Equal(host.ConnectionData, join.HostConnectionData);
            var e = await Assert.ThrowsAsync<RelayServiceException>(() => client.JoinAsync("BBBBBB"));
            Assert.Equal(404, e.Status);
        }

        // ── Froglet's UDP transport through the relay ───────────────

        static NetEvent Next(INetTransport t, NetEventKind kind, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (t.Poll(out var e)) { if (e.Kind == kind) return e; continue; }
                System.Threading.Thread.Sleep(1);
            }
            throw new TimeoutException($"no {kind} event");
        }

        (UdpTransport host, List<(UdpTransport t, int peer)> clients) Party(int clients)
        {
            var hostAlloc = server.Allocate(clients);
            server.CreateJoinCode(hostAlloc.AllocationId);
            var host = UdpTransport.Over(new RelayLink(hostAlloc, host: true), server: true);
            var list = new List<(UdpTransport, int)>();
            for (int i = 0; i < clients; i++)
            {
                var c = UdpTransport.Over(new RelayLink(server.Join(hostAlloc.JoinCode), host: false), server: false, 5000);
                Next(c, NetEventKind.Connected);
                list.Add((c, Next(host, NetEventKind.Connected).Peer));
            }
            return (host, list);
        }

        [Fact]
        public void AHostAndThreeClients_ExchangeFramesThroughTheRelay()
        {
            var (host, clients) = Party(3);
            using (host)
            {
                foreach (var (c, peer) in clients)
                {
                    c.Send(0, new byte[] { (byte)peer });
                    var e = Next(host, NetEventKind.Data);
                    Assert.Equal(peer, e.Peer);
                    Assert.Equal(new[] { (byte)peer }, e.Payload);
                    host.Send(peer, new byte[] { 99, (byte)peer });
                    Assert.Equal(new byte[] { 99, (byte)peer }, Next(c, NetEventKind.Data).Payload);
                }
                Assert.True(server.Forwarded > 0);
                foreach (var (c, _) in clients) c.Dispose();
            }
        }

        [Fact]
        public void ThroughTheRelay_UnderLoss_ABigFrameArrivesWhole()
        {
            var (host, clients) = Party(1);
            var (c, peer) = clients[0];
            using (host) using (c)
            {
                host.DropPercent = 15; c.DropPercent = 15;
                var big = new byte[300_000];
                new Random(3).NextBytes(big);
                host.Send(peer, big);
                Assert.Equal(big, Next(c, NetEventKind.Data, 30000).Payload);
            }
        }

        [Fact]
        public void AJoinCodeForAHostThatLeft_FailsTheConnect()
        {
            var hostAlloc = server.Allocate(3);
            server.CreateJoinCode(hostAlloc.AllocationId); // the host never binds
            using var c = UdpTransport.Over(new RelayLink(server.Join(hostAlloc.JoinCode), host: false), server: false, 1500);
            Next(c, NetEventKind.Disconnected, 5000);
        }

        [Fact]
        public async Task TheDriver_ApprovesAClientThroughTheRelay()
        {
            // The whole path the game takes: REST allocation, a host listening through the relay factory,
            // a client connecting to "relay" with its join allocation.
            var api = new RelayAllocationClient(server.BaseUrl);
            var hostAlloc = await api.AllocateAsync(3);
            await api.CreateJoinCodeAsync(hostAlloc);
            var factory = new RelayTransportFactory(new UdpTransportFactory());
            RelaySessions.HostWith(hostAlloc);
            using var host = factory.Listen("0.0.0.0", 7777);
            RelaySessions.JoinWith(await api.JoinAsync(hostAlloc.JoinCode));
            using var client = factory.Connect(RelaySessions.RelayAddress, 0, 5000);
            Next(client, NetEventKind.Connected);
            Next(host, NetEventKind.Connected);
            client.Send(0, new byte[] { 1, 2, 3 });
            Assert.Equal(new byte[] { 1, 2, 3 }, Next(host, NetEventKind.Data).Payload);
            Assert.Null(RelaySessions.PendingHost);
            Assert.Null(RelaySessions.PendingJoin);
        }
    }
}
