using System.Collections.Generic;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The network simulator on a fake clock over the loopback: each setting does what
    /// docs/MULTIPLAYER.md §6.2 says, and the transport contract holds under all of them.
    /// </summary>
    public class SimulatedTransportTests
    {
        sealed class Rig
        {
            public double Now;
            public NetSimSettings Settings = new();
            public SimulatedTransport Server, Client;
            public int Peer;

            public Rig(NetSimSettings serverSide = null)
            {
                var f = new LoopbackTransportFactory();
                var inner = f.Listen("127.0.0.1", 0);
                // Only the server is simulated; the client is a plain pass-through on the same clock.
                Server = new SimulatedTransport(inner, () => Settings, () => Now, seed: 7);
                Client = new SimulatedTransport(f.Connect("127.0.0.1", inner.ListenPort, 1000), () => new NetSimSettings(), () => Now, seed: 9);
                Assert.True(Drain(Client).Exists(e => e.Kind == NetEventKind.Connected));
                Peer = Drain(Server).Find(e => e.Kind == NetEventKind.Connected).Peer;
                Assert.NotEqual(0, Peer);
                if (serverSide != null) Settings = serverSide;
            }

            public void Advance(double ms) => Now += ms;

            public static List<NetEvent> Drain(INetTransport t)
            {
                var list = new List<NetEvent>();
                while (t.Poll(out var e)) list.Add(e);
                return list;
            }
        }

        [Fact]
        public void Off_PassesStraightThrough()
        {
            var r = new Rig();
            r.Client.Send(0, new byte[] { 1 });
            Assert.Single(Rig.Drain(r.Server));
        }

        [Fact]
        public void Latency_DelaysBothDirections()
        {
            var r = new Rig(new NetSimSettings { LatencyMs = 50 });
            r.Client.Send(0, new byte[] { 1 });
            Assert.Empty(Rig.Drain(r.Server));       // arriving: held 50 ms
            r.Advance(49);
            Assert.Empty(Rig.Drain(r.Server));
            r.Advance(1);
            Assert.Single(Rig.Drain(r.Server));

            r.Server.Send(r.Peer, new byte[] { 2 }); // leaving: held 50 ms
            Assert.Empty(Rig.Drain(r.Client));
            r.Advance(50);
            r.Server.Poll(out _);                    // the driver polls every frame; that releases held sends
            Assert.Single(Rig.Drain(r.Client));
        }

        [Fact]
        public void Jitter_NeverReorders()
        {
            var r = new Rig(new NetSimSettings { LatencyMs = 10, JitterMs = 200, LossPercent = 30 });
            for (byte i = 0; i < 100; i++) { r.Client.Send(0, new[] { i }); r.Advance(1); }
            var got = new List<byte>();
            for (int t = 0; t < 2000 && got.Count < 100; t++)
            {
                r.Advance(1);
                foreach (var e in Rig.Drain(r.Server)) got.Add(e.Payload[0]);
            }
            Assert.Equal(100, got.Count);
            for (int i = 0; i < 100; i++) Assert.Equal(i, got[i]);
        }

        [Fact]
        public void Bandwidth_SpacesFramesByTheirSize()
        {
            // 80 kbps = 80 bits/ms: a 1000-byte frame occupies the line for 100 ms.
            var r = new Rig(new NetSimSettings { BandwidthKbps = 80 });
            r.Server.Send(r.Peer, new byte[1000]);
            r.Server.Send(r.Peer, new byte[1000]);
            r.Advance(100); r.Server.Poll(out _);
            Assert.Single(Rig.Drain(r.Client));
            r.Advance(99); r.Server.Poll(out _);
            Assert.Empty(Rig.Drain(r.Client));
            r.Advance(1); r.Server.Poll(out _);
            Assert.Single(Rig.Drain(r.Client));
        }

        [Fact]
        public void KickReason_ArrivesBeforeTheClose()
        {
            var r = new Rig(new NetSimSettings { LatencyMs = 30 });
            r.Server.Send(r.Peer, new byte[] { 9 });
            r.Server.Disconnect(r.Peer);
            r.Advance(30); r.Server.Poll(out _);
            var events = Rig.Drain(r.Client);
            Assert.Equal(NetEventKind.Data, events[0].Kind);
            Assert.Equal(NetEventKind.Disconnected, events[1].Kind);
        }

        [Fact]
        public void Down_HoldsTraffic_ThenUp_DeliversIt()
        {
            var r = new Rig(new NetSimSettings { Down = true });
            r.Client.Send(0, new byte[] { 1 });
            r.Server.Send(r.Peer, new byte[] { 2 });
            r.Advance(5000);
            Assert.Empty(Rig.Drain(r.Server));
            Assert.Empty(Rig.Drain(r.Client));
            r.Settings = new NetSimSettings();
            Assert.Single(Rig.Drain(r.Server));
            Assert.Single(Rig.Drain(r.Client));
        }

        [Fact]
        public void DownPastTheTimeout_DropsEveryPeer_OnBothEnds()
        {
            var r = new Rig(new NetSimSettings { Down = true });
            r.Server.Poll(out _);
            r.Advance(NetSimulator.DownDisconnectMs + 1);
            var serverSide = Rig.Drain(r.Server);
            Assert.Contains(serverSide, e => e.Kind == NetEventKind.Disconnected && e.Peer == r.Peer);
            Assert.Contains(Rig.Drain(r.Client), e => e.Kind == NetEventKind.Disconnected);
            Assert.Equal(0, r.Server.PeerCount);
        }

        [Theory]
        [InlineData("4g", 60, 20, 1f, 0, false)]
        [InlineData("poor loss=10", 200, 80, 10f, 128, false)]
        [InlineData("latency=80,jitter=5", 80, 5, 0f, 0, false)]
        [InlineData("broadband down", 20, 5, 0f, 0, true)]
        [InlineData("3g off", 0, 0, 0f, 0, false)]
        public void Spec_Parses(string spec, int latency, int jitter, float loss, int bandwidth, bool down)
        {
            Assert.True(NetSimulator.TryParse(spec, new NetSimSettings(), out var s, out var error), error);
            Assert.Equal((latency, jitter, loss, bandwidth, down), (s.LatencyMs, s.JitterMs, s.LossPercent, s.BandwidthKbps, s.Down));
        }

        [Theory]
        [InlineData("latency=-5")]
        [InlineData("speed=3")]
        [InlineData("dialup")]
        public void BadSpec_IsRefused_AndChangesNothing(string spec)
        {
            var before = new NetSimSettings { LatencyMs = 33 };
            Assert.False(NetSimulator.TryParse(spec, before, out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.Equal(33, before.LatencyMs);
        }
    }
}
