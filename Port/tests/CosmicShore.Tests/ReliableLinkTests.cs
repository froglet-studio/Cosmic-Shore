using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Prisma's reliability layer (<see cref="UdpTransport"/>) over a link that loses, duplicates and
    /// reorders datagrams - the guarantees the Relay transport inherits, since a relay only forwards
    /// datagrams (docs/RELAY.md). The same code runs over Unity Relay; only the link differs.
    /// </summary>
    public class ReliableLinkTests
    {
        static (UdpTransport server, UdpTransport client, EventPump s, EventPump c, int peer) Connect(LossyDatagramNetwork net, int silenceMs = UdpTransport.TimeoutMs)
        {
            var sn = net.Add("server");
            var cn = net.Add("client");
            var server = UdpTransport.ListenOver(sn);
            var client = UdpTransport.ConnectOver(cn, sn.Address, 10000);
            server.SilenceTimeoutMs = silenceMs;
            client.SilenceTimeoutMs = silenceMs;
            var s = new EventPump(server);
            var c = new EventPump(client);
            c.Next(NetEventKind.Connected);
            int peer = s.Next(NetEventKind.Connected).Peer;
            return (server, client, s, c, peer);
        }

        static byte[] Frame(int index, int size)
        {
            var b = new byte[Math.Max(8, size)];
            new Random(index).NextBytes(b);
            BitConverter.TryWriteBytes(b, index);
            return b;
        }

        [Fact]
        public void ReliableFrames_ArriveWholeAndInOrder_ThroughLossDuplicationAndReordering()
        {
            var net = new LossyDatagramNetwork { LossPercent = 20, DuplicatePercent = 10, ReorderPercent = 30 };
            var (server, client, s, c, peer) = Connect(net);
            using (server) using (client)
            {
                var sizes = new Random(3);
                var toServer = Enumerable.Range(0, 150).Select(i => Frame(i, sizes.Next(1, 6000))).ToList();
                var toClient = Enumerable.Range(0, 150).Select(i => Frame(1000 + i, sizes.Next(1, 6000))).ToList();
                foreach (var f in toServer) client.Send(0, f);
                foreach (var f in toClient) server.Send(peer, f);

                foreach (var expected in toServer)
                    Assert.Equal(expected, s.Next(e => e.Kind == NetEventKind.Data, 30000).Payload);
                foreach (var expected in toClient)
                    Assert.Equal(expected, c.Next(e => e.Kind == NetEventKind.Data, 30000).Payload);
                Assert.True(server.Resends > 0 && client.Resends > 0, "the loss should have forced resends");
                Assert.True(net.Duplicated > 0);
                Assert.Equal(1, server.PeerCount); // a duplicated ConnectRequest is still one peer
            }
        }

        [Fact]
        public void AMegabyteFrame_IsFragmentedIntoPacketsUnderTheCap_AndReassembledThroughLoss()
        {
            var net = new LossyDatagramNetwork { LossPercent = 10, ReorderPercent = 20 };
            var (server, client, s, c, peer) = Connect(net);
            using (server) using (client)
            {
                var big = RandomNumberGenerator.GetBytes(1024 * 1024);
                client.Send(0, big);
                Assert.Equal(big, s.Next(NetEventKind.Data, 60000).Payload);
                // Every datagram stays inside one ~1,200-byte packet (the payload cap the Relay transport relies on).
                Assert.True(net.LargestDatagram <= UdpTransport.MaxPacket, $"largest datagram {net.LargestDatagram} B");
                Assert.True(UdpTransport.MaxPacket <= 1200);
            }
        }

        [Fact]
        public void UnreliableFrames_AreNeverResent_OrHeldBehindReliableOnes()
        {
            var net = new LossyDatagramNetwork { LossPercent = 25, ReorderPercent = 20 };
            var (server, client, s, c, peer) = Connect(net);
            using (server) using (client)
            {
                var sent = new HashSet<int>();
                for (int i = 0; i < 300; i++)
                {
                    var f = Frame(i, 200);
                    sent.Add(i);
                    server.Send(peer, f, f.Length, NetChannel.Unreliable);
                    if (i % 30 == 0) server.Send(peer, Frame(10000 + i, 3000)); // reliable traffic alongside
                    System.Threading.Thread.Sleep(1);
                }
                var got = c.Drain(NetEventKind.Data, 3000);
                var unreliable = got.Where(e => e.Channel == NetChannel.Unreliable).ToList();
                var reliable = got.Where(e => e.Channel == NetChannel.Reliable).Select(e => BitConverter.ToInt32(e.Payload)).ToList();
                foreach (var e in unreliable)
                {
                    int index = BitConverter.ToInt32(e.Payload);
                    Assert.Contains(index, sent);
                    Assert.Equal(Frame(index, 200), e.Payload); // whole, never corrupted
                }
                Assert.InRange(unreliable.Count, 150, 299); // about a quarter lost, and those never come back
                Assert.Equal(Enumerable.Range(0, 10).Select(k => 10000 + k * 30), reliable); // reliable: all, in order
            }
        }

        [Fact]
        public void ALineThatGoesSilent_TimesOutOnBothEnds()
        {
            var net = new LossyDatagramNetwork();
            var (server, client, s, c, peer) = Connect(net, silenceMs: 600);
            using (server) using (client)
            {
                net.Silent = true;
                Assert.Equal(peer, s.Next(NetEventKind.Disconnected, 5000).Peer);
                Assert.Equal(0, c.Next(NetEventKind.Disconnected, 5000).Peer);
            }
        }

        [Fact]
        public void AServerKick_DeliversQueuedFramesFirst_ThenDisconnectsTheClient()
        {
            var net = new LossyDatagramNetwork { LossPercent = 10 };
            var (server, client, s, c, peer) = Connect(net);
            using (server) using (client)
            {
                server.Send(peer, new byte[] { 42, 1, 2, 3 });
                server.Disconnect(peer);
                Assert.Equal(new byte[] { 42, 1, 2, 3 }, c.Next(NetEventKind.Data).Payload);
                c.Next(NetEventKind.Disconnected);
                s.Next(NetEventKind.Disconnected);
            }
        }

        [Fact]
        public void KeepalivesHoldAnIdleConnectionOpen()
        {
            var net = new LossyDatagramNetwork { LossPercent = 20 };
            var (server, client, s, c, peer) = Connect(net, silenceMs: 1500);
            using (server) using (client)
            {
                Assert.Empty(c.Drain(NetEventKind.Disconnected, 4000)); // idle for 2.6x the timeout
                Assert.Equal(1, server.PeerCount);
                Assert.Equal(1, client.PeerCount);
            }
        }

        [Fact]
        public void ALinkFault_DisconnectsEveryPeer()
        {
            var link = new FaultingLink();
            using var server = UdpTransport.ListenOver(link);
            var pump = new EventPump(server);
            link.Fault = "the relay refused us";
            pump.Drain(NetEventKind.Disconnected, 300); // no peers yet: nothing to report, and the thread stops
            using var client = UdpTransport.ConnectOver(new FaultingLink { Fault = "no route" }, new MemEndPoint("x"), 5000);
            Assert.Equal(0, new EventPump(client).Next(NetEventKind.Disconnected, 3000).Peer); // a client hears its connect fail
        }

        sealed class FaultingLink : IDatagramLink
        {
            public volatile string FaultText;
            public string Fault { get => FaultText; set => FaultText = value; }
            public int Receive(byte[] buffer, int waitMicros, out System.Net.EndPoint from) { from = null; System.Threading.Thread.Sleep(1); return -1; }
            public void Send(byte[] buffer, int length, System.Net.EndPoint to) { }
            public void Service(double nowMs) { }
            public bool IsServer(System.Net.EndPoint from, System.Net.EndPoint server) => false;
            public int LocalPort => 0;
            public void Dispose() { }
        }
    }
}
