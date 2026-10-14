using System;
using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Froglet's UDP transport under real datagram loss (docs/MULTIPLAYER.md §6.6): every frame still
    /// arrives whole and in order, a big frame survives fragmentation, a silent peer times out, and a
    /// spoofed packet from another address is ignored. The contract checks run in NetTransportContractTests.
    /// </summary>
    public class UdpTransportTests
    {
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

        static (UdpTransport server, UdpTransport client, int peer) Pair()
        {
            var server = UdpTransport.Listen("127.0.0.1", 0);
            var client = UdpTransport.Connect("127.0.0.1", server.ListenPort, 3000);
            Next(client, NetEventKind.Connected);
            int peer = Next(server, NetEventKind.Connected).Peer;
            return (server, client, peer);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(30)]
        public void UnderLoss_EveryFrameArrives_WholeAndInOrder(double lossPercent)
        {
            var (server, client, peer) = Pair();
            using (server) using (client)
            {
                server.DropPercent = lossPercent;
                client.DropPercent = lossPercent;
                var rng = new Random(5);
                var sent = new List<byte[]>();
                for (int i = 0; i < 300; i++)
                {
                    var b = new byte[i == 150 ? 200_000 : 1 + rng.Next(3000)]; // one frame of ~175 fragments
                    rng.NextBytes(b);
                    b[0] = (byte)i;
                    sent.Add(b);
                    client.Send(0, b);
                }
                foreach (var expected in sent)
                {
                    var e = Next(server, NetEventKind.Data, 30000);
                    Assert.Equal(peer, e.Peer);
                    Assert.Equal(expected, e.Payload);
                }
                Assert.True(client.Resends > 0, "loss was configured but nothing was resent");
            }
        }

        [Fact]
        public void ASilentPeer_TimesOut_OnBothEnds()
        {
            var (server, client, peer) = Pair();
            using (server) using (client)
            {
                server.SilenceTimeoutMs = 600;
                client.SilenceTimeoutMs = 600;
                client.DropPercent = 100; // the client's line goes dead: no keepalives reach the server
                server.DropPercent = 100;
                Assert.Equal(peer, Next(server, NetEventKind.Disconnected, 5000).Peer);
                Next(client, NetEventKind.Disconnected, 5000);
                Assert.Equal(0, server.PeerCount);
            }
        }

        [Fact]
        public void AnIdleConnection_StaysUp_OnKeepalives()
        {
            var (server, client, _) = Pair();
            using (server) using (client)
            {
                server.SilenceTimeoutMs = 1000;
                client.SilenceTimeoutMs = 1000;
                System.Threading.Thread.Sleep(2500); // nothing sent by either side for 2.5 timeouts
                Assert.False(server.Poll(out var e) && e.Kind == NetEventKind.Disconnected);
                Assert.Equal(1, server.PeerCount);
                client.Send(0, new byte[] { 7 });
                Assert.Equal(new byte[] { 7 }, Next(server, NetEventKind.Data).Payload);
            }
        }

        [Fact]
        public void APacketFromAnotherAddress_IsIgnored()
        {
            var (server, client, _) = Pair();
            using (server) using (client)
            {
                // A stranger sends a well-formed Data packet with a guessed token: nothing reaches the game.
                using var stranger = new System.Net.Sockets.UdpClient(0);
                var forged = new byte[21 + 7 + 3];
                forged[0] = 3;
                BitConverter.TryWriteBytes(forged.AsSpan(1), 12345UL);
                BitConverter.TryWriteBytes(forged.AsSpan(21 + 5), (ushort)3);
                forged[21 + 4] = 1;
                stranger.Send(forged, forged.Length, "127.0.0.1", server.ListenPort);
                System.Threading.Thread.Sleep(100);
                Assert.False(server.Poll(out _));
                Assert.Equal(1, server.PeerCount);
            }
        }

        [Fact]
        public void Dispose_FlushesWhatWasQueued_BeforeSayingGoodbye()
        {
            var (server, client, _) = Pair();
            using (client)
            {
                server.DropPercent = 20;
                for (byte i = 0; i < 50; i++) server.Send(1, new[] { i });
                server.Dispose(); // like closing a TCP socket: the queued frames still go out
                for (byte i = 0; i < 50; i++) Assert.Equal(new[] { i }, Next(client, NetEventKind.Data).Payload);
                Next(client, NetEventKind.Disconnected);
            }
        }
    }
}
