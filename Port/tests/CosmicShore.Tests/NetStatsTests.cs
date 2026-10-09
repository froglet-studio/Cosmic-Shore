using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="NetStats"/>: what the driver sends and receives is counted per peer, per kind and per
    /// RPC method; RTT is measured and reported; a capture lands as JSON (docs/MULTIPLAYER.md §6.4).
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class NetStatsTests : IDisposable
    {
        const byte ConnectRequest = 1, Rpc = 9, TimePing = 19;

        readonly GameLoop loop = new(nameof(NetStatsTests));
        readonly LoopbackTransportFactory factory = new();
        readonly NetworkManager nm;

        public NetStatsTests()
        {
            NetDriver.TransportFactory = factory;
            nm = new GameObject("nm").AddComponent<NetworkManager>();
            nm.IsServer = false;
            nm.IsClient = false;
            nm.IsListening = false;
            nm.ConnectedClientsIds.Clear();
        }

        public void Dispose()
        {
            NetDriver.Stop();
            NetDriver.TransportFactory = new TcpTransportFactory();
            NetworkManager.Singleton = null;
            NetStats.Reset();
            loop.Dispose();
        }

        static void Drain(INetTransport t) { while (t.Poll(out _)) { } }

        (INetTransport client, NetStats.PeerStats peer) ApprovedClient()
        {
            Assert.True(NetDriver.StartServer(nm, "0.0.0.0", 7777));
            var client = factory.Connect("127.0.0.1", 7777, 1000);
            Drain(client);
            client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 });
            NetDriver.EarlyUpdate();
            Drain(client);
            return (client, NetStats.Peers.Single(p => p.ClientId == 1));
        }

        [Fact]
        public void TheHandshake_IsCounted_UnderTheApprovedClientId()
        {
            var (client, peer) = ApprovedClient();
            using (client)
            {
                // The request arrived before the client had an id; its bytes moved to client 1 on approval.
                Assert.Equal(1, peer.MsgsIn);
                Assert.Equal(5, peer.BytesIn);
                Assert.True(peer.MsgsOut >= 1); // ConnectAccept at least
                Assert.Equal(NetStats.MsgsOut, NetStats.Peers.Sum(p => p.MsgsOut));
                Assert.Contains("ConnectRequest 0/1", NetStats.Report());
            }
        }

        [Fact]
        public void Rpcs_AreCounted_ByMethodName()
        {
            var (client, _) = ApprovedClient();
            using (client)
            {
                using var ms = new MemoryStream();
                using (var w = new BinaryWriter(ms))
                {
                    w.Write(Rpc); w.Write(99UL); w.Write((ushort)0); w.Write("FireMissileServerRpc");
                }
                var payload = ms.ToArray();
                client.Send(0, payload);
                client.Send(0, payload);
                NetDriver.EarlyUpdate(); // the object does not exist: the RPC is dropped, but it was received
                var json = JsonDocument.Parse(NetStats.Command("json")).RootElement;
                var rpc = json.GetProperty("rpcsIn").EnumerateArray().Single();
                Assert.Equal("FireMissileServerRpc", rpc.GetProperty("method").GetString());
                Assert.Equal(2, rpc.GetProperty("calls").GetInt64());
            }
        }

        [Fact]
        public void AClientsReportedRtt_ReachesTheHostsStats()
        {
            var (client, peer) = ApprovedClient();
            using (client)
            {
                Assert.Equal(-1, peer.RttMs);
                using var ms = new MemoryStream();
                using (var w = new BinaryWriter(ms)) { w.Write(TimePing); w.Write(1.0); w.Write(87.5); }
                client.Send(0, ms.ToArray());
                NetDriver.EarlyUpdate();
                Assert.Equal(87.5, peer.RttMs);
                Assert.Contains("rtt  87.5 ms (last 88, min 88)", NetStats.Report());
            }
        }

        [Fact]
        public void AnOldPing_WithoutAnRtt_IsStillAnswered()
        {
            var (client, peer) = ApprovedClient();
            using (client)
            {
                using var ms = new MemoryStream();
                using (var w = new BinaryWriter(ms)) { w.Write(TimePing); w.Write(1.0); }
                client.Send(0, ms.ToArray());
                NetDriver.EarlyUpdate();
                Assert.True(client.Poll(out var e));
                Assert.Equal(20, e.Payload[0]); // TimePong
                Assert.Equal(-1, peer.RttMs);
            }
        }

        [Fact]
        public void RttSamples_AreSmoothed()
        {
            NetStats.Reset();
            NetStats.RttSample(0, 100);
            NetStats.RttSample(0, 180);
            var p = NetStats.Peers.Single();
            Assert.Equal(120, p.RttMs); // 100 + (180-100)/4
            Assert.Equal((180, 100), (p.LastRttMs, p.MinRttMs));
        }

        [Fact]
        public void Capture_WritesOneRowPerFrame_ThenStops()
        {
            var path = Path.Combine(Path.GetTempPath(), $"netstats-test-{Guid.NewGuid():N}.json");
            try
            {
                var (client, _) = ApprovedClient();
                using (client)
                {
                    Assert.Contains(path, NetStats.Command($"capture 3 {path}"));
                    for (int i = 0; i < 5; i++) NetDriver.EarlyUpdate();
                    var doc = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                    Assert.Equal("prisma-net-capture", doc.GetProperty("kind").GetString());
                    Assert.Equal(3, doc.GetProperty("frames").GetArrayLength());
                    Assert.Equal("server", doc.GetProperty("summary").GetProperty("role").GetString());
                }
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void StartingASession_ResetsTheCounters()
        {
            var (client, _) = ApprovedClient();
            client.Dispose();
            Assert.True(NetStats.Any);
            NetDriver.Stop();
            Assert.True(NetDriver.StartServer(nm, "0.0.0.0", 7778));
            Assert.False(NetStats.Any);
        }

        [Fact]
        public void Title_NamesTheProfile_AndTheRole()
        {
            Assert.Equal("Cosmic Shore · PilotB", NetStats.Title("PilotB"));
            var (client, _) = ApprovedClient();
            using (client) Assert.StartsWith("Cosmic Shore · PilotA · HOST · ", NetStats.Title("PilotA"));
        }
    }
}
