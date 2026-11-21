using System;
using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Networking.Components;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The unreliable channel (docs/MULTIPLAYER.md §6.6) at each layer: UDP sends it once and never
    /// resends it, TCP falls back to reliable, the simulator really loses it, and a NetworkTransform
    /// that opts in drops stale poses and sends its settled pose reliably.
    /// </summary>
    public class UnreliableChannelTests
    {
        static NetEvent Next(INetTransport t, NetEventKind kind, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (t.Poll(out var e)) { if (e.Kind == kind) return e; continue; }
                System.Threading.Thread.Sleep(1);
            }
            throw new TimeoutException($"no {kind} event");
        }

        static (INetTransport server, INetTransport client, int peer) Pair(INetTransportFactory f)
        {
            var server = f.Listen("127.0.0.1", 0);
            var client = f.Connect("127.0.0.1", server.ListenPort, 3000);
            Next(client, NetEventKind.Connected);
            int peer = Next(server, NetEventKind.Connected).Peer;
            return (server, client, peer);
        }

        [Fact]
        public void Udp_DeliversAnUnreliableFrame_MarkedUnreliable()
        {
            var (server, client, _) = Pair(new UdpTransportFactory());
            using (server) using (client)
            {
                client.Send(0, new byte[] { 1, 2, 3 }, -1, NetChannel.Unreliable);
                var e = Next(server, NetEventKind.Data);
                Assert.Equal(new byte[] { 1, 2, 3 }, e.Payload);
                Assert.Equal(NetChannel.Unreliable, e.Channel);
            }
        }

        [Fact]
        public void Udp_NeverResendsAnUnreliableFrame_AndItHoldsNothingBack()
        {
            var (server, client, _) = Pair(new UdpTransportFactory());
            using (server) using (client)
            {
                var udp = (UdpTransport)client;
                udp.DropPercent = 100;
                for (int i = 0; i < 20; i++) client.Send(0, new byte[] { (byte)i }, -1, NetChannel.Unreliable);
                System.Threading.Thread.Sleep(400);
                Assert.Equal(0, udp.Resends);
                Assert.Equal(20, udp.UnreliableSent);
                udp.DropPercent = 0;
                client.Send(0, new byte[] { 99 });
                Assert.Equal(new byte[] { 99 }, Next(server, NetEventKind.Data).Payload); // the lost ones never come
            }
        }

        [Fact]
        public void Udp_AnUnreliableFrameTooBigForOnePacket_GoesReliably()
        {
            var (server, client, _) = Pair(new UdpTransportFactory());
            using (server) using (client)
            {
                var big = new byte[UdpTransport.FragmentBytes + 1];
                big[0] = 42;
                client.Send(0, big, -1, NetChannel.Unreliable);
                var e = Next(server, NetEventKind.Data);
                Assert.Equal(big, e.Payload);
                Assert.Equal(NetChannel.Reliable, e.Channel);
            }
        }

        [Fact]
        public void Tcp_FallsBackToReliable()
        {
            var (server, client, _) = Pair(new TcpTransportFactory());
            using (server) using (client)
            {
                client.Send(0, new byte[] { 5 }, -1, NetChannel.Unreliable);
                var e = Next(server, NetEventKind.Data);
                Assert.Equal(new byte[] { 5 }, e.Payload);
                Assert.Equal(NetChannel.Reliable, e.Channel);
            }
        }

        [Fact]
        public void Simulator_LosesUnreliableFrames_ButStillDeliversReliableOnes()
        {
            var settings = new NetSimSettings { LossPercent = 100 };
            var (server, client, _) = Pair(new SimulatedTransportFactory(new UdpTransportFactory(), () => settings));
            using (server) using (client)
            {
                for (int i = 0; i < 10; i++) client.Send(0, new byte[] { 1 }, -1, NetChannel.Unreliable);
                client.Send(0, new byte[] { 2 }); // reliable: "lost" means late, never gone
                var e = Next(server, NetEventKind.Data);
                Assert.Equal(new byte[] { 2 }, e.Payload);
                Assert.Equal(10, ((SimulatedTransport)client).UnreliableDropped);
            }
        }

        [Fact]
        public void Simulator_DoesNotHoldUnreliableFramesBehindADelayedReliableOne()
        {
            double now = 0;
            var settings = new NetSimSettings { LatencyMs = 10 };
            var f = new LoopbackTransportFactory();
            var innerServer = f.Listen("127.0.0.1", 0);
            var server = new SimulatedTransport(innerServer, () => new NetSimSettings(), () => now);
            var client = new SimulatedTransport(f.Connect("127.0.0.1", innerServer.ListenPort, 1000), () => settings, () => now, seed: 3);
            using (server) using (client)
            {
                while (client.Poll(out _)) { }
                while (server.Poll(out _)) { }
                settings.LossPercent = 100; // the reliable frame is "lost": it arrives a resend later (2*10+20 = 40 ms extra)
                client.Send(0, new byte[] { 1 });
                settings.LossPercent = 0;
                client.Send(0, new byte[] { 2 }, -1, NetChannel.Unreliable);
                now = 10; client.Poll(out _);
                Assert.True(server.Poll(out var first));
                Assert.Equal(new byte[] { 2 }, first.Payload); // the unreliable frame is not stuck behind the late reliable one
                now = 50; client.Poll(out _);
                Assert.True(server.Poll(out var second));
                Assert.Equal(new byte[] { 1 }, second.Payload);
            }
        }

        [Fact]
        public void Transform_DropsAStalePose_ButAlwaysTakesATeleport()
        {
            using var loop = new GameLoop(nameof(Transform_DropsAStalePose_ButAlwaysTakesATeleport));
            var nt = new GameObject("fish").AddComponent<NetworkTransform>();
            Assert.True(nt.PortAcceptStamp(10.0, teleport: false));
            Assert.False(nt.PortAcceptStamp(9.5, teleport: false));  // arrived late: a newer pose is already applied
            Assert.False(nt.PortAcceptStamp(10.0, teleport: false)); // a duplicate
            Assert.True(nt.PortAcceptStamp(9.0, teleport: true));    // a teleport always applies
            Assert.True(nt.PortAcceptStamp(10.5, teleport: false));
            Assert.Equal(2, nt.PortStale);
        }

        [Fact]
        public void Transform_SendsItsSettledPoseReliably_OnceAfterAQuarterSecond()
        {
            using var loop = new GameLoop(nameof(Transform_SendsItsSettledPoseReliably_OnceAfterAQuarterSecond));
            var nt = new GameObject("fish").AddComponent<NetworkTransform>();
            nt.UseUnreliableDeltas = true;
            nt.transform.position = new Vector3(1, 2, 3);
            Assert.True(nt.PortTakeOutgoing(out _, out _, out _, out _));
            nt.PortMarkSent(100.0, unreliable: true);
            Assert.False(nt.PortNeedsKeyframe(100.1, out _, out _, out _)); // still moving, as far as anyone knows
            Assert.True(nt.PortNeedsKeyframe(100.3, out var p, out _, out _));
            Assert.Equal(new Vector3(1, 2, 3), p);
            Assert.False(nt.PortNeedsKeyframe(101.0, out _, out _, out _)); // once
            nt.PortMarkSent(102.0, unreliable: false);
            Assert.False(nt.PortNeedsKeyframe(103.0, out _, out _, out _)); // a reliable last send needs no keyframe
        }
    }
}
