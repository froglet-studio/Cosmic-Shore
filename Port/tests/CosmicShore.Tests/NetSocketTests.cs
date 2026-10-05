using System;
using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>The TCP transport on loopback: framing, ordering and disconnect events.</summary>
    public class NetSocketTests
    {
        static NetSocket.Event Next(NetSocket s, NetSocket.EventKind kind, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (s.Poll(out var e))
                {
                    if (e.Kind == kind) return e;
                    continue;
                }
                System.Threading.Thread.Sleep(1);
            }
            throw new TimeoutException($"no {kind} event");
        }

        [Fact]
        public void Frames_ArriveWhole_InOrder_BothWays()
        {
            using var server = NetSocket.Listen("127.0.0.1", 0);
            Assert.True(server.ListenPort > 0);
            using var client = NetSocket.Connect("127.0.0.1", server.ListenPort, 3000);
            Next(client, NetSocket.EventKind.Connected);
            int peer = Next(server, NetSocket.EventKind.Connected).Peer;

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
                Assert.Equal(expected, Next(server, NetSocket.EventKind.Data).Payload);

            server.Send(peer, new byte[] { 42, 43 });
            Assert.Equal(new byte[] { 42, 43 }, Next(client, NetSocket.EventKind.Data).Payload);

            client.Disconnect(0);
            Assert.Equal(peer, Next(server, NetSocket.EventKind.Disconnected).Peer);
            Assert.Equal(0, server.PeerCount);
        }

        [Fact]
        public void SecondListener_OnATakenPort_Fails()
        {
            using var first = NetSocket.Listen("127.0.0.1", 0);
            Assert.ThrowsAny<Exception>(() => NetSocket.Listen("127.0.0.1", first.ListenPort).Dispose());
        }

        [Fact]
        public void ConnectToNobody_ReportsDisconnected()
        {
            int port;
            using (var probe = NetSocket.Listen("127.0.0.1", 0)) port = probe.ListenPort;
            using var client = NetSocket.Connect("127.0.0.1", port, 1000);
            Next(client, NetSocket.EventKind.Disconnected);
        }
    }
}
