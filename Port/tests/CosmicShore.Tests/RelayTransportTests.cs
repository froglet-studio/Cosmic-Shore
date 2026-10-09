using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using CosmicShore.Engine.Networking;
using CosmicShore.Online;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The "relay" transport end to end against <see cref="FakeRelayServer"/>: Prisma's reliability layer over
    /// the Relay protocol (BIND, CONNECT_REQUEST/ACCEPTED, RELAY, PING, CLOSE) on the loopback, plain UDP.
    /// The live run against Unity's servers (with DTLS) is RelayLiveTests.
    /// </summary>
    public class RelayTransportTests : IDisposable
    {
        readonly FakeRelayServer relay = new();
        readonly RelayTransportFactory factory = new() { Dtls = false };

        public RelayTransportTests()
        {
            factory.Opener = (a, dtls) => () => new UdpRelayChannel("127.0.0.1", relay.Port);
            factory.SetupTimeoutMs = 3000;
        }

        public void Dispose()
        {
            relay.Dispose();
            NetRelay.InUse = false;
            NetRelay.JoinCode = null;
        }

        (INetTransport server, EventPump s, RelayAllocation hostAlloc) Host()
        {
            var a = relay.AllocateHost();
            factory.ArmHost(a);
            var server = factory.Listen("0.0.0.0", 7777);
            Assert.True(NetRelay.InUse);
            return (server, new EventPump(server), a);
        }

        (INetTransport client, EventPump c) Join(RelayAllocation host)
        {
            factory.ArmJoin(relay.Join(host));
            var client = factory.Connect("127.0.0.1", 7777, 5000);
            return (client, new EventPump(client));
        }

        [Fact]
        public void HostAndJoiner_ExchangeFrames_ThroughTheRelay_AndTheHostLearnsThePeerFromItsFirstMessage()
        {
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                Assert.Equal(0, server.ListenPort); // nothing listens locally: the relay is the address
                var (client, c) = Join(hostAlloc);
                using (client)
                {
                    c.Next(NetEventKind.Connected);
                    int peer = s.Next(NetEventKind.Connected).Peer;
                    Assert.Equal(2, relay.Binds);
                    Assert.Equal(1, relay.Accepts);

                    var big = RandomNumberGenerator.GetBytes(50_000); // fragmented across ~45 RELAY messages
                    client.Send(0, big);
                    server.Send(peer, new byte[] { 1, 2, 3 });
                    server.Send(peer, new byte[] { 9 }, 1, NetChannel.Unreliable);
                    Assert.Equal(big, s.Next(NetEventKind.Data).Payload);
                    var got = c.Drain(NetEventKind.Data, 500);
                    Assert.Contains(got, e => e.Channel == NetChannel.Reliable && e.Payload.SequenceEqual(new byte[] { 1, 2, 3 }));
                    Assert.Contains(got, e => e.Channel == NetChannel.Unreliable && e.Payload.SequenceEqual(new byte[] { 9 }));
                    Assert.True(relay.LargestRelayContent <= UdpTransport.MaxPacket, $"RELAY content {relay.LargestRelayContent} B");
                    Assert.Equal(0, relay.NotConnectedErrors);
                }
            }
        }

        [Fact]
        public void TwoJoiners_AreTwoPeers_OnOneHostAllocation()
        {
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                var (c1, p1) = Join(hostAlloc);
                var (c2, p2) = Join(hostAlloc);
                using (c1) using (c2)
                {
                    p1.Next(NetEventKind.Connected);
                    p2.Next(NetEventKind.Connected);
                    int a = s.Next(NetEventKind.Connected).Peer, b = s.Next(NetEventKind.Connected).Peer;
                    Assert.NotEqual(a, b);
                    Assert.Equal(2, server.PeerCount);
                    c1.Send(0, new byte[] { 1 });
                    c2.Send(0, new byte[] { 2 });
                    var e1 = s.Next(NetEventKind.Data);
                    var e2 = s.Next(NetEventKind.Data);
                    Assert.Equal(new[] { (a, (byte)1), (b, (byte)2) }.OrderBy(x => x.Item2),
                                 new[] { (e1.Peer, e1.Payload[0]), (e2.Peer, e2.Payload[0]) }.OrderBy(x => x.Item2));
                }
            }
        }

        [Fact]
        public void FramesSurviveARelayThatDropsDatagrams()
        {
            relay.DropPercent = 15;
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                var (client, c) = Join(hostAlloc);
                using (client)
                {
                    c.Next(NetEventKind.Connected, 15000);
                    int peer = s.Next(NetEventKind.Connected, 15000).Peer;
                    for (int i = 0; i < 100; i++) server.Send(peer, BitConverter.GetBytes(i));
                    for (int i = 0; i < 100; i++) Assert.Equal(i, BitConverter.ToInt32(c.Next(NetEventKind.Data, 20000).Payload));
                }
            }
        }

        [Fact]
        public void APlayerMismatchError_RebindsWithTheNextNonce()
        {
            relay.MismatchFirstBind = true;
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                var (client, c) = Join(hostAlloc);
                using (client)
                {
                    c.Next(NetEventKind.Connected);
                    s.Next(NetEventKind.Connected);
                    Assert.Equal(2, relay.Binds);
                }
            }
        }

        [Fact]
        public void TheLinkPingsTheRelayServer_AndMeasuresItsRoundTrip()
        {
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (factory.LastLink.ServerRttMs < 0 && DateTime.UtcNow < deadline) Thread.Sleep(20);
                Assert.True(relay.Pings >= 1);
                Assert.InRange(factory.LastLink.ServerRttMs, 0, 500);
            }
        }

        [Fact]
        public void Disposing_ClosesTheAllocations()
        {
            var (server, s, hostAlloc) = Host();
            var (client, c) = Join(hostAlloc);
            c.Next(NetEventKind.Connected);
            s.Next(NetEventKind.Connected);
            client.Dispose();
            s.Next(NetEventKind.Disconnected, 5000); // the client's goodbye arrived through the relay
            server.Dispose();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (relay.Closes < 2 && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Assert.Equal(2, relay.Closes);
        }

        [Fact]
        public void ARelayServerClose_DisconnectsTheClient()
        {
            var (server, s, hostAlloc) = Host();
            using (server)
            {
                var joinAlloc = relay.Join(hostAlloc);
                factory.ArmJoin(joinAlloc);
                using var client = factory.Connect("127.0.0.1", 7777, 5000);
                var c = new EventPump(client);
                c.Next(NetEventKind.Connected);
                relay.CloseAllocation(joinAlloc);
                c.Next(NetEventKind.Disconnected, 5000);
            }
        }

        [Fact]
        public void AJoinToAnAllocationNobodyAnswers_FailsAfterTheSetupTimeout()
        {
            var bogus = new RelayAllocation(Guid.NewGuid(), new byte[16], new byte[255], new byte[64], new byte[50], "x",
                new[] { new RelayEndpoint("udp", "127.0.0.1", relay.Port, false) });
            factory.SetupTimeoutMs = 800;
            factory.ArmJoin(bogus);
            using var client = factory.Connect("127.0.0.1", 7777, 200);
            new EventPump(client).Next(NetEventKind.Disconnected, 5000);
        }

        [Fact]
        public void UnarmedListenAndConnect_FallBackToPlainUdp()
        {
            using var server = factory.Listen("127.0.0.1", 0);
            Assert.False(NetRelay.InUse);
            Assert.True(server.ListenPort > 0);
            using var client = factory.Connect("127.0.0.1", server.ListenPort, 3000);
            new EventPump(client).Next(NetEventKind.Connected);
        }
    }
}
