using System;
using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The <see cref="INetTransport"/> contract, checked against every implementation: TCP (the
    /// shipping one), the in-memory loopback, and both behind the network simulator. A new transport (the internet relay of C6) joins
    /// <see cref="Transports"/> and must pass the same checks.
    /// </summary>
    public class NetTransportContractTests
    {
        public static TheoryData<string> Transports => new() { "tcp", "loopback", "sim-tcp", "sim-loopback" };

        /// <summary>A bad line: the simulator must keep the contract (whole, ordered, reliable) under it.</summary>
        static readonly NetSimSettings BadLine = new() { LatencyMs = 5, JitterMs = 10, LossPercent = 20, BandwidthKbps = 200000 };

        static INetTransportFactory Make(string name) => name switch
        {
            "tcp" => new TcpTransportFactory(),
            "loopback" => new LoopbackTransportFactory(),
            "sim-tcp" => new SimulatedTransportFactory(new TcpTransportFactory(), () => BadLine),
            "sim-loopback" => new SimulatedTransportFactory(new LoopbackTransportFactory(), () => BadLine),
            _ => throw new ArgumentException(name),
        };

        static NetEvent Next(INetTransport t, NetEventKind kind, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (t.Poll(out var e))
                {
                    if (e.Kind == kind) return e;
                    continue;
                }
                System.Threading.Thread.Sleep(1);
            }
            throw new TimeoutException($"no {kind} event");
        }

        static (INetTransport server, INetTransport client, int peer) Pair(INetTransportFactory f)
        {
            var server = f.Listen("127.0.0.1", 0);
            Assert.True(server.IsServer);
            Assert.True(server.ListenPort > 0);
            var client = f.Connect("127.0.0.1", server.ListenPort, 3000);
            Assert.False(client.IsServer);
            Next(client, NetEventKind.Connected);
            int peer = Next(server, NetEventKind.Connected).Peer;
            Assert.NotEqual(0, peer);
            return (server, client, peer);
        }

        [Theory, MemberData(nameof(Transports))]
        public void Frames_ArriveWhole_InOrder_BothWays(string name)
        {
            var (server, client, peer) = Pair(Make(name));
            using (server) using (client)
            {
                var sent = new List<byte[]>();
                var rng = new Random(7);
                foreach (int size in new[] { 1, 9, 4096, 1 << 20, 3 })
                {
                    var b = new byte[size];
                    rng.NextBytes(b);
                    sent.Add(b);
                    client.Send(0, b);
                }
                foreach (var expected in sent)
                {
                    var e = Next(server, NetEventKind.Data);
                    Assert.Equal(peer, e.Peer);
                    Assert.Equal(expected, e.Payload);
                }

                server.Send(peer, new byte[] { 42, 43 });
                Assert.Equal(new byte[] { 42, 43 }, Next(client, NetEventKind.Data).Payload);
            }
        }

        [Theory, MemberData(nameof(Transports))]
        public void Send_WithLength_SendsOnlyThePrefix(string name)
        {
            var (server, client, peer) = Pair(Make(name));
            using (server) using (client)
            {
                client.Send(0, new byte[] { 1, 2, 3, 4, 5 }, 2);
                Assert.Equal(new byte[] { 1, 2 }, Next(server, NetEventKind.Data).Payload);
            }
        }

        [Theory, MemberData(nameof(Transports))]
        public void ClientDisconnect_ReachesTheServer_WithItsPeer(string name)
        {
            var (server, client, peer) = Pair(Make(name));
            using (server) using (client)
            {
                client.Disconnect(0);
                Assert.Equal(peer, Next(server, NetEventKind.Disconnected).Peer);
                Assert.Equal(0, server.PeerCount);
            }
        }

        [Theory, MemberData(nameof(Transports))]
        public void ServerKick_ReachesTheClient(string name)
        {
            var (server, client, peer) = Pair(Make(name));
            using (server) using (client)
            {
                server.Disconnect(peer);
                Next(client, NetEventKind.Disconnected);
                Assert.Equal(0, server.PeerCount);
            }
        }

        [Theory, MemberData(nameof(Transports))]
        public void ServerDispose_ReachesTheClient(string name)
        {
            var (server, client, _) = Pair(Make(name));
            using (client)
            {
                server.Dispose();
                Next(client, NetEventKind.Disconnected);
            }
        }

        [Theory, MemberData(nameof(Transports))]
        public void SecondListener_OnATakenPort_Throws(string name)
        {
            var f = Make(name);
            using var first = f.Listen("127.0.0.1", 0);
            Assert.ThrowsAny<Exception>(() => f.Listen("127.0.0.1", first.ListenPort).Dispose());
        }

        [Theory, MemberData(nameof(Transports))]
        public void ConnectToNobody_ReportsDisconnected_WithoutThrowing(string name)
        {
            var f = Make(name);
            int port;
            using (var probe = f.Listen("127.0.0.1", 0)) port = probe.ListenPort;
            using var client = f.Connect("127.0.0.1", port, 1000);
            Next(client, NetEventKind.Disconnected);
        }
    }
}
