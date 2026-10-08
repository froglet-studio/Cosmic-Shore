using System;
using System.IO;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class NetDriverStaticsCollection { public const string Name = "NetDriver statics"; }

    /// <summary>
    /// NetDriver opens and drives its transport only through <see cref="NetDriver.TransportFactory"/>:
    /// run over the in-memory loopback, it listens, falls back to port 0, runs the connect handshake
    /// from both ends and disposes the transport on Stop - with no socket anywhere.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class NetDriverTransportTests : IDisposable
    {
        // NetDriver's Msg values for the two handshake messages these tests read and write.
        const byte ConnectRequest = 1, ConnectAccept = 2;

        readonly GameLoop loop = new(nameof(NetDriverTransportTests));
        readonly LoopbackTransportFactory factory = new();
        readonly NetworkManager nm;

        public NetDriverTransportTests()
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
            loop.Dispose();
        }

        static NetEvent Next(INetTransport t, NetEventKind kind)
        {
            while (t.Poll(out var e)) if (e.Kind == kind) return e;
            throw new InvalidOperationException($"no {kind} event queued");
        }

        [Fact]
        public void StartServer_ListensThroughTheFactory()
        {
            Assert.True(NetDriver.StartServer(nm, "0.0.0.0", 7777));
            Assert.True(NetDriver.IsServer);
            Assert.Equal(7777, NetDriver.ListenPort);
            Assert.Equal(new[] { 7777 }, factory.Listens);
        }

        [Fact]
        public void StartServer_OnATakenPort_FallsBackToAnyPort()
        {
            factory.FailListenPort = 7777;
            Assert.True(NetDriver.StartServer(nm, "0.0.0.0", 7777));
            Assert.Equal(new[] { 7777, 0 }, factory.Listens);
            Assert.True(NetDriver.ListenPort > 0);
            Assert.NotEqual(7777, NetDriver.ListenPort);
        }

        [Fact]
        public void Stop_DisposesTheTransport()
        {
            NetDriver.StartServer(nm, "0.0.0.0", 7777);
            var transport = Assert.Single(factory.Opened);
            NetDriver.Stop();
            Assert.False(NetDriver.IsActive);
            Assert.True(transport.Disposed);
        }

        [Fact]
        public void Client_OnConnected_SendsItsApprovalPayload()
        {
            using var server = factory.Listen("127.0.0.1", 7777);
            NetDriver.StartClient(nm, "127.0.0.1", 7777, new byte[] { 9, 8, 7 });
            Assert.True(NetDriver.IsClientOnly);
            NetDriver.EarlyUpdate();

            Next(server, NetEventKind.Connected);
            using var r = new BinaryReader(new MemoryStream(Next(server, NetEventKind.Data).Payload));
            Assert.Equal(ConnectRequest, r.ReadByte());
            Assert.Equal(new byte[] { 9, 8, 7 }, r.ReadBytes(r.ReadInt32()));
        }

        [Fact]
        public void Server_ApprovesAConnectRequest_AndAnswersWithTheClientId()
        {
            NetDriver.StartServer(nm, "0.0.0.0", 7777);
            using var client = factory.Connect("127.0.0.1", 7777, 1000);
            Next(client, NetEventKind.Connected);
            client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 });
            NetDriver.EarlyUpdate();

            using var r = new BinaryReader(new MemoryStream(Next(client, NetEventKind.Data).Payload));
            Assert.Equal(ConnectAccept, r.ReadByte());
            Assert.Equal(1UL, r.ReadUInt64());
            Assert.Contains(1UL, nm.ConnectedClientsIds);
        }

        [Fact]
        public void Server_OverTcp_ApprovesARealSocketClient()
        {
            NetDriver.TransportFactory = new TcpTransportFactory();
            Assert.True(NetDriver.StartServer(nm, "127.0.0.1", 0));
            using var client = NetSocket.Connect("127.0.0.1", NetDriver.ListenPort, 3000);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool sent = false;
            while (sw.ElapsedMilliseconds < 5000)
            {
                NetDriver.EarlyUpdate();
                if (client.Poll(out var e))
                {
                    if (e.Kind == NetEventKind.Connected && !sent) { client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 }); sent = true; }
                    else if (e.Kind == NetEventKind.Data)
                    {
                        Assert.Equal(ConnectAccept, e.Payload[0]);
                        Assert.Equal(1UL, BitConverter.ToUInt64(e.Payload, 1));
                        return;
                    }
                }
                System.Threading.Thread.Sleep(1);
            }
            throw new TimeoutException("no ConnectAccept over TCP");
        }

        [Fact]
        public void Server_BehindTheSimulator_ApprovesARealSocketClient_AfterTheAddedDelay()
        {
            var saved = NetSimulator.Settings;
            try
            {
                NetSimulator.Settings = new NetSimSettings { LatencyMs = 40 };
                NetDriver.TransportFactory = new TcpTransportFactory();
                NetSimulator.Install();
                NetSimulator.Install(); // idempotent: one wrapper, not two
                Assert.IsType<SimulatedTransportFactory>(NetDriver.TransportFactory);
                Assert.True(NetDriver.StartServer(nm, "127.0.0.1", 0));
                using var client = NetSocket.Connect("127.0.0.1", NetDriver.ListenPort, 3000);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                long sentAt = -1;
                while (sw.ElapsedMilliseconds < 5000)
                {
                    NetDriver.EarlyUpdate();
                    if (client.Poll(out var e))
                    {
                        if (e.Kind == NetEventKind.Connected && sentAt < 0) { client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 }); sentAt = sw.ElapsedMilliseconds; }
                        else if (e.Kind == NetEventKind.Data)
                        {
                            Assert.Equal(ConnectAccept, e.Payload[0]);
                            // The server sits behind 40 ms each way: the answer takes at least the round trip.
                            Assert.True(sw.ElapsedMilliseconds - sentAt >= 80, $"answered after {sw.ElapsedMilliseconds - sentAt} ms");
                            return;
                        }
                    }
                    System.Threading.Thread.Sleep(1);
                }
                throw new TimeoutException("no ConnectAccept behind the simulator");
            }
            finally { NetSimulator.Settings = saved; }
        }

        [Fact]
        public void Client_WhoseConnectFails_StopsAndReportsTheReason()
        {
            NetDriver.StartClient(nm, "127.0.0.1", 7777, null); // nobody listens on the loopback
            NetDriver.EarlyUpdate();
            Assert.False(NetDriver.IsActive);
            Assert.Equal("disconnected from server", nm.DisconnectReason);
        }
    }
}
