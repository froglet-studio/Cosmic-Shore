using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// How a player's networking is put together (<see cref="NetStack"/>), and that the player does it. The merge of
    /// the online server into Ys-bleeding-edge (61da694263) dropped the transport choice, the relay, the network
    /// simulator and session faults from PlayerBoot while every engine test stayed green: each layer was tested
    /// alone, nothing tested the composition. These do, and the online server's lobby carrying a match over the
    /// Unity-Relay-protocol relay is played end to end.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class NetStackTests : IDisposable
    {
        readonly bool _savedEmulate = NetworkManager.EmulateNetcodeLifecycle;
        readonly bool _savedEnabled = NetDriver.Enabled;
        IDisposable[] _cleanup = Array.Empty<IDisposable>();

        public NetStackTests()
        {
            NetworkManager.Singleton = null;
            RelaySessions.Reset();
            NetDriver.TransportFactory = new TcpTransportFactory();
        }

        public void Dispose()
        {
            NetDriver.Stop();
            foreach (var d in _cleanup.Reverse()) try { d.Dispose(); } catch (Exception) { }
            NetDriver.TransportFactory = new TcpTransportFactory();
            RelaySessions.Reset();
            MultiplayerService.Instance = null;
            NetworkManager.Singleton = null;
            NetworkManager.EmulateNetcodeLifecycle = _savedEmulate;
            NetDriver.Enabled = _savedEnabled;
        }

        static OnlineMultiplayerService Service(string player, OnlineServer server, bool drive) =>
            new(new OnlineClientOptions
            {
                Server = $"127.0.0.1:{server.Port}", PlayerId = () => player, AllowEmbeddedServer = false, Log = _ => { },
            }) { DriveNetworkManager = drive };

        [Fact]
        public void TheOrder_TransportThenRelayThenSessions_ThenSimulatorAndFaultsAroundThem()
        {
            using var server = OnlineServer.Start(new OnlineServerOptions { Port = 0, Discovery = false, Log = _ => { } });
            OnlineMultiplayerService online = null;
            NetStack.Install("udp", () => RelaySessions.Install(new RelayAllocationClient("http://127.0.0.1:9")), () =>
            {
                online = Service("alice", server, drive: false);
                MultiplayerService.Instance = online;
                NetDriver.TransportFactory = new OnlineTransportFactory(online, NetDriver.TransportFactory);
            });
            _cleanup = new IDisposable[] { online };
            Assert.Equal("simulated > online > relay > udp", NetStack.Describe());
            Assert.Equal("faults > online", NetStack.DescribeSessions());
        }

        [Fact]
        public void TheSharedFolder_KeepsTheChosenTransport_TcpWithoutARelay()
        {
            var dir = Path.Combine(Path.GetTempPath(), "netstack-" + Guid.NewGuid().ToString("N"));
            NetStack.Install("tcp", null, () => MultiplayerService.Instance = new DirectoryMultiplayerService(dir));
            Assert.Equal("simulated > tcp", NetStack.Describe());
            Assert.Equal("faults > folder", NetStack.DescribeSessions());
            NetStack.Install("", null, () => MultiplayerService.Instance = new DirectoryMultiplayerService(dir));
            Assert.Equal("simulated > udp", NetStack.Describe()); // UDP is the default; installing twice does not stack layers
            Assert.Equal("faults > folder", NetStack.DescribeSessions());
        }

        [Fact]
        public void OnlineInstall_WrapsTheChosenTransport_InsteadOfReplacingIt()
        {
            using var server = OnlineServer.Start(new OnlineServerOptions { Port = 0, Discovery = false, Log = _ => { } });
            NetTransports.Select("udp");
            var online = OnlineMultiplayerService.Install(new OnlineClientOptions
            {
                Server = $"127.0.0.1:{server.Port}", PlayerId = () => "alice", AllowEmbeddedServer = false, Log = _ => { },
            });
            _cleanup = new IDisposable[] { online };
            Assert.Equal("online > udp", NetStack.Describe());
        }

        [Fact]
        public void ThePlayer_PutsItsNetworkingTogetherThroughNetStack()
        {
            var boot = File.ReadAllText(Path.Combine(PortRoot(), "src", "CosmicShore.Player", "PlayerBoot.cs"));
            Assert.Contains("NetStack.Install(", boot);
            Assert.Contains("COSMIC_SHORE_NET_TRANSPORT", boot);
            Assert.Contains("InstallRelay", boot);
            // Nothing else in the player may pick the transport or the simulator/faults on its own: that is how they were lost.
            var stray = Regex.Matches(boot, @"NetTransports\.Select|NetSimulator\.Install|NetFaults\.Install|TransportFactory\s*=").Select(m => m.Value).ToList();
            Assert.True(stray.Count == 0, "PlayerBoot sets networking layers outside NetStack: " + string.Join(", ", stray));
        }

        [Fact]
        public async Task TheOnlineLobby_CarriesAMatch_OverTheUnityRelayProtocolRelay()
        {
            const byte ConnectRequest = 1, ConnectAccept = 2;
            using var relayServer = FrogletRelayServer.Start();
            using var server = OnlineServer.Start(new OnlineServerOptions { Port = 0, Discovery = false, Log = _ => { } });
            var loop = new GameLoop(nameof(TheOnlineLobby_CarriesAMatch_OverTheUnityRelayProtocolRelay));
            NetworkManager.EmulateNetcodeLifecycle = true;
            NetDriver.Enabled = true;

            OnlineMultiplayerService alice = null;
            NetStack.Install("udp", () => RelaySessions.Install(new RelayAllocationClient(relayServer.BaseUrl)), () =>
            {
                alice = Service("alice", server, drive: true);
                MultiplayerService.Instance = alice;
                NetDriver.TransportFactory = new OnlineTransportFactory(alice, NetDriver.TransportFactory);
            });
            var bob = Service("bob", server, drive: false);
            alice.Start();
            bob.Start();
            _cleanup = new IDisposable[] { loop, alice, bob };

            var nm = new GameObject("nm").AddComponent<NetworkManager>();
            NetworkManager.Singleton = nm;
            var hosted = await alice.CreateSessionAsync(new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork());
            Assert.True(nm.IsListening);

            // The lobby is the online server; the record it hands a joiner names the relay, not the server's own relay.
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            var record = ((OnlineMultiplayerService.OnlineSession)joined).LastRecord;
            Assert.False(string.IsNullOrEmpty(record.RelayJoinCode));

            await RelayedSessions.JoinAsync(record.RelayJoinCode);
            using var client = new OnlineTransportFactory(bob, new RelayTransportFactory(new UdpTransportFactory()))
                .Connect(RelaySessions.RelayAddress, 0, 8000);
            var sw = Stopwatch.StartNew();
            bool sent = false;
            while (sw.ElapsedMilliseconds < 15000)
            {
                NetDriver.EarlyUpdate();
                if (client.Poll(out var e))
                {
                    if (e.Kind == NetEventKind.Connected && !sent) { client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 }); sent = true; }
                    else if (e.Kind == NetEventKind.Data && e.Payload[0] == ConnectAccept) return;
                    else if (e.Kind == NetEventKind.Disconnected) throw new InvalidOperationException("the relay link closed");
                }
                Thread.Sleep(1);
            }
            throw new TimeoutException("no ConnectAccept through the relay");
        }

        static string PortRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "src", "CosmicShore.Player"))) return d.FullName;
            throw new DirectoryNotFoundException("Port/ not found above " + AppContext.BaseDirectory);
        }
    }
}
