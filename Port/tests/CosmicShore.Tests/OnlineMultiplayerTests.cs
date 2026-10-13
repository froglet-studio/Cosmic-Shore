using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The online server and the game's side of it, over real sockets on this machine: lobby
    /// semantics between "machines" (separate service instances with their own player ids),
    /// reconnects that re-assert sessions, a server standing down for a better one, the relay
    /// carrying netcode frames and NetDriver's handshake, keys, and LAN discovery with the
    /// automatic mode's one-server-per-machine election. Discovery runs on a private UDP port so
    /// a game running on the machine cannot interfere.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class OnlineMultiplayerTests : IDisposable
    {
        readonly List<IDisposable> _cleanup = new();
        readonly int _savedDiscoveryPort = OnlineWire.DiscoveryPort;

        public OnlineMultiplayerTests()
        {
            NetworkManager.Singleton = null;
            OnlineWire.DiscoveryPort = 48000 + Environment.ProcessId % 1000;
        }

        public void Dispose()
        {
            foreach (var d in Enumerable.Reverse(_cleanup))
                try { d.Dispose(); } catch (Exception) { }
            OnlineWire.DiscoveryPort = _savedDiscoveryPort;
            NetDriver.Stop();
            NetDriver.TransportFactory = new TcpTransportFactory();
            NetworkManager.Singleton = null;
        }

        OnlineServer Server(bool standalone = true, string key = null, int port = 0, bool discovery = false)
        {
            var s = OnlineServer.Start(new OnlineServerOptions { Port = port, Key = key, Standalone = standalone, Discovery = discovery, Log = _ => { } });
            _cleanup.Add(s);
            return s;
        }

        OnlineMultiplayerService Client(string player, string server, string key = null, bool embed = false, int connectWaitMs = 8000)
        {
            var c = new OnlineMultiplayerService(new OnlineClientOptions
            {
                Server = server, Key = key, PlayerId = () => player, AllowEmbeddedServer = embed,
                ConnectWaitMs = connectWaitMs, RequestTimeoutMs = 5000, DiscoveryMs = 400, Log = _ => { },
            });
            c.Start();
            _cleanup.Add(c);
            return c;
        }

        OnlineMultiplayerService Client(string player, OnlineServer server, string key = null, int connectWaitMs = 8000)
            => Client(player, $"127.0.0.1:{server.Port}", key, connectWaitMs: connectWaitMs);

        static SessionOptions Options(string mode, int max = 4, string name = null, bool relay = false)
        {
            var o = new SessionOptions
            {
                MaxPlayers = max,
                SessionProperties = new Dictionary<string, SessionProperty> { ["gameMode"] = new(mode, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                PlayerProperties = name == null ? null : new Dictionary<string, PlayerProperty> { ["displayName"] = new(name, VisibilityPropertyOptions.Member) },
            };
            return relay ? o.WithRelayNetwork() : o;
        }

        static QuerySessionsOptions Query(string mode)
        {
            var q = new QuerySessionsOptions();
            q.FilterOptions.Add(new FilterOption(FilterField.StringIndex1, mode, FilterOperation.Equal));
            return q;
        }

        static void WaitFor(Func<bool> condition, string what, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("timed out waiting for " + what);
                Thread.Sleep(20);
            }
        }

        static NetEvent Next(INetTransport t, NetEventKind kind, int timeoutMs = 8000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (t.Poll(out var e)) { if (e.Kind == kind) return e; continue; }
                Thread.Sleep(2);
            }
            throw new TimeoutException($"no {kind} event");
        }

        // ── Lobby between machines ──────────────────────────────────

        [Fact]
        public async Task ASessionCreatedOnOneMachine_IsFoundJoinedAndSharedFromAnother()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);

            var hosted = await alice.CreateSessionAsync(Options("presence", max: 4, name: "Alice"));
            Assert.True(hosted.IsHost);

            var found = await bob.QuerySessionsAsync(Query("presence"));
            var info = Assert.Single(found.Sessions);
            Assert.Equal(hosted.Id, info.Id);
            Assert.Equal(4, info.MaxPlayers);
            Assert.Equal(3, info.AvailableSlots);
            Assert.Empty((await bob.QuerySessionsAsync(Query("match"))).Sessions);

            var joined = await bob.JoinSessionByIdAsync(info.Id, new JoinSessionOptions
            {
                PlayerProperties = new Dictionary<string, PlayerProperty> { ["displayName"] = new("Bob", VisibilityPropertyOptions.Member) },
            });
            Assert.False(joined.IsHost);
            Assert.Equal(new[] { "alice", "bob" }, joined.Players.Select(p => p.Id));
            Assert.Equal("Alice", joined.Players[0].Properties["displayName"].Value);

            // The invite channel: a per-player property one machine writes and the other reads.
            joined.CurrentPlayer.SetProperty("invite_payloads", new PlayerProperty("{\"to\":\"alice\"}", VisibilityPropertyOptions.Member));
            await joined.SaveCurrentPlayerDataAsync();
            await hosted.RefreshAsync();
            var seenBob = hosted.Players.Single(p => p.Id == "bob");
            Assert.Equal("Bob", seenBob.Properties["displayName"].Value);
            Assert.Equal("{\"to\":\"alice\"}", seenBob.Properties["invite_payloads"].Value);
            Assert.Equal("presence", ((OnlineMultiplayerService.OnlineSession)hosted).Properties["gameMode"].Value);
        }

        [Fact]
        public async Task Queries_HidePrivateAndLockedSessions_AndJoinsRefuseFullOnes()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var carol = Client("carol", server);

            var priv = Options("party");
            priv.IsPrivate = true;
            var hiddenSession = await alice.CreateSessionAsync(priv);
            Assert.Empty((await bob.QuerySessionsAsync(Query("party"))).Sessions);
            // Private is unlisted, not unjoinable: the invite flow joins by id.
            await bob.JoinSessionByIdAsync(hiddenSession.Id);

            var duo = await carol.CreateSessionAsync(Options("duo", max: 2));
            await bob.JoinSessionByIdAsync(duo.Id);
            var full = await Assert.ThrowsAsync<SessionException>(() => alice.JoinSessionByIdAsync(duo.Id));
            Assert.Contains("full", full.Message);

            var missing = await Assert.ThrowsAsync<SessionException>(() => alice.JoinSessionByIdAsync("no-such-session"));
            Assert.Equal(SessionError.SessionNotFound, missing.Error);
        }

        [Fact]
        public async Task HostLeaving_HandsTheSessionOn_AndMembersHearWhoLeft()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            var left = new List<string>();
            joined.PlayerLeaving += left.Add;

            await hosted.LeaveAsync();
            Assert.Null(hosted.CurrentPlayer);
            await joined.RefreshAsync();
            Assert.True(joined.IsHost);
            Assert.Equal(new[] { "alice" }, left);
        }

        [Fact]
        public async Task HostDelete_ReachesMembers_AsSessionDeleted_AndOnlyTheHostMayDelete()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);

            // A member's delete is refused by the server (the handle still closes locally, as the SDK's does).
            var stillThere = await alice.JoinSessionByIdAsync(hosted.Id);
            await joined.AsHost().DeleteAsync();
            await stillThere.RefreshAsync();
            Assert.Equal(new[] { "alice", "bob" }, stillThere.Players.Select(p => p.Id));

            int deleted = 0;
            var again = await bob.JoinSessionByIdAsync(hosted.Id);
            again.Deleted += () => deleted++;
            await hosted.AsHost().DeleteAsync();
            var e = await Assert.ThrowsAsync<SessionException>(() => again.RefreshAsync());
            Assert.Equal(SessionError.SessionDeleted, e.Error);
            Assert.Equal(1, deleted);
            Assert.Null(again.CurrentPlayer);
        }

        [Fact]
        public async Task RemovedPlayer_HearsNotInLobby()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);

            await Assert.ThrowsAsync<SessionException>(() => joined.AsHost().RemovePlayerAsync("alice"));
            await hosted.AsHost().RemovePlayerAsync("bob");
            var e = await Assert.ThrowsAsync<SessionException>(() => joined.RefreshAsync());
            Assert.Equal(SessionError.NotInLobby, e.Error);
        }

        [Fact]
        public async Task ADroppedPlayer_KeepsItsSeat_ForTheReconnectGrace()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            bob.Dispose();
            WaitFor(() => server.Status.StartsWith("1 player"), "bob's connection to close");
            await hosted.RefreshAsync();
            Assert.Contains(hosted.Players, p => p.Id == "bob");
        }

        [Fact]
        public void AControlConnectionThatGoesSilent_IsDropped()
        {
            int saved = OnlineServer.ControlSilenceMs;
            OnlineServer.ControlSilenceMs = 400;
            try
            {
                var server = Server();
                // A game that vanished without closing: it said hello, then nothing, not even pings.
                using var ghost = OnlineWire.Connect("127.0.0.1", server.Port, 2000);
                OnlineWire.WriteFrame(ghost.GetStream(), new OnlineMessage { Op = "hello", V = OnlineWire.Version, Player = "ghost" });
                Assert.True(OnlineWire.ReadFrame(ghost.GetStream()).Ok);
                WaitFor(() => server.Status.StartsWith("1 player"), "the ghost to be connected");
                WaitFor(() => server.Status.StartsWith("0 player"), "the silent connection to be dropped", 5000);
            }
            finally { OnlineServer.ControlSilenceMs = saved; }
        }

        // ── Reconnects and moves ───────────────────────────────────

        [Fact]
        public async Task ServerRestart_ClientsReassert_AndTheSessionComesBackWhole()
        {
            var first = Server();
            int port = first.Port;
            var alice = Client("alice", first);
            var bob = Client("bob", first);
            var hosted = await alice.CreateSessionAsync(Options("party", name: "Alice"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);

            first.Dispose();
            WaitFor(() => !alice.IsConnected && !bob.IsConnected, "both clients to notice the server is gone");
            var second = Server(port: port);
            WaitFor(() => alice.IsConnected && bob.IsConnected, "both clients to reconnect", 20000);
            WaitFor(() => second.Status.Contains("1 session"), "the session to be re-asserted");

            await joined.RefreshAsync();
            Assert.Equal("alice", joined.Players[0].Id);
            Assert.False(joined.IsHost);
            Assert.Equal(new[] { "alice", "bob" }, joined.Players.Select(p => p.Id));
            Assert.Equal("Alice", joined.Players[0].Properties["displayName"].Value);
            var found = await bob.QuerySessionsAsync(Query("party"));
            Assert.Equal(hosted.Id, Assert.Single(found.Sessions).Id);
            await hosted.RefreshAsync();
            Assert.True(hosted.IsHost);
        }

        [Fact]
        public async Task ARefreshDuringAReconnect_WaitsForTheReassert_InsteadOfHearingDeleted()
        {
            var first = Server();
            int port = first.Port;
            var alice = Client("alice", first);
            var bob = Client("bob", first);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            int deleted = 0;
            joined.Deleted += () => deleted++;

            first.Dispose();
            WaitFor(() => !bob.IsConnected, "bob to notice the server is gone");
            // Asked while there is no server at all: the call waits for the reconnect AND the
            // re-assert, so the brand-new server already holds the session when the refresh lands.
            var refresh = joined.RefreshAsync();
            Server(port: port);
            await refresh;
            Assert.Equal(0, deleted);
            Assert.Equal(new[] { "alice", "bob" }, joined.Players.Select(p => p.Id));
        }

        [Fact]
        public async Task AServerThatStandsDown_SendsEveryoneToTheBetterOne_WithTheirSessions()
        {
            var embedded = Server(standalone: false);
            var better = Server(standalone: true);
            var alice = Client("alice", embedded);
            var bob = Client("bob", embedded);
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);

            // What hearing the better server's LAN announcement does (the UDP part is covered below).
            embedded.ConsiderMerge(better.Me, IPAddress.Loopback);
            Assert.True(embedded.Yielded);
            WaitFor(() => alice.ConnectedServer == $"127.0.0.1:{better.Port}" && bob.ConnectedServer == $"127.0.0.1:{better.Port}", "both clients to move");
            WaitFor(() => better.Status.StartsWith("2 player") && better.Status.Contains("1 session"), "the sessions to arrive");
            await joined.RefreshAsync();
            Assert.Equal(new[] { "alice", "bob" }, joined.Players.Select(p => p.Id));

            // A standalone server never stands down.
            better.ConsiderMerge(new ServerAnnouncement { V = OnlineWire.Version, Id = "0", Port = embedded.Port, Started = 1, Standalone = true }, IPAddress.Loopback);
            Assert.False(better.Yielded);
        }

        // ── Relay ───────────────────────────────────────────────────

        [Fact]
        public async Task Relay_CarriesNetcodeFrames_BetweenTheHostAndAJoiner()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party", relay: true));
            using var host = new OnlineTransportFactory(alice).Listen("127.0.0.1", 0);
            await bob.JoinSessionByIdAsync(hosted.Id);

            using var client = new OnlineTransportFactory(bob).Connect(OnlineWire.RelayScheme + hosted.Id, 0, 8000);
            Next(client, NetEventKind.Connected);
            int peer = Next(host, NetEventKind.Connected).Peer;
            Assert.NotEqual(0, peer);

            var big = new byte[1 << 20];
            new System.Random(3).NextBytes(big);
            client.Send(0, new byte[] { 1, 2, 3 });
            client.Send(0, big);
            Assert.Equal(new byte[] { 1, 2, 3 }, Next(host, NetEventKind.Data).Payload);
            Assert.Equal(big, Next(host, NetEventKind.Data).Payload);
            host.Send(peer, new byte[] { 9 });
            Assert.Equal(new byte[] { 9 }, Next(client, NetEventKind.Data).Payload);

            client.Disconnect(0);
            Assert.Equal(peer, Next(host, NetEventKind.Disconnected).Peer);
        }

        [Fact]
        public async Task Relay_ToAHostThatIsNotListening_ReportsDisconnected()
        {
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party", relay: true));
            await bob.JoinSessionByIdAsync(hosted.Id);
            using var client = new OnlineTransportFactory(bob).Connect(OnlineWire.RelayScheme + hosted.Id, 0, 8000);
            Next(client, NetEventKind.Disconnected);

            // A session without a Relay network has nothing to connect to.
            var lobby = await alice.CreateSessionAsync(Options("presence"));
            await bob.JoinSessionByIdAsync(lobby.Id);
            using var none = new OnlineTransportFactory(bob).Connect(OnlineWire.RelayScheme + lobby.Id, 0, 8000);
            Next(none, NetEventKind.Disconnected);
        }

        [Fact]
        public async Task NetDriver_ApprovesAClient_ThatConnectsThroughTheRelay()
        {
            const byte ConnectRequest = 1, ConnectAccept = 2;
            var loop = new GameLoop(nameof(NetDriver_ApprovesAClient_ThatConnectsThroughTheRelay));
            _cleanup.Add(loop);
            var server = Server();
            var alice = Client("alice", server);
            var bob = Client("bob", server);
            var hosted = await alice.CreateSessionAsync(Options("party", relay: true));

            NetDriver.TransportFactory = new OnlineTransportFactory(alice);
            var nm = new GameObject("nm").AddComponent<NetworkManager>();
            nm.IsServer = false;
            nm.IsClient = false;
            nm.IsListening = false;
            nm.ConnectedClientsIds.Clear();
            Assert.True(NetDriver.StartServer(nm, "127.0.0.1", 0));

            await bob.JoinSessionByIdAsync(hosted.Id);
            using var client = new OnlineTransportFactory(bob).Connect(OnlineWire.RelayScheme + hosted.Id, 0, 8000);
            var sw = Stopwatch.StartNew();
            bool sent = false;
            while (sw.ElapsedMilliseconds < 10000)
            {
                NetDriver.EarlyUpdate();
                if (client.Poll(out var e))
                {
                    if (e.Kind == NetEventKind.Connected && !sent) { client.Send(0, new byte[] { ConnectRequest, 0, 0, 0, 0 }); sent = true; }
                    else if (e.Kind == NetEventKind.Data)
                    {
                        Assert.Equal(ConnectAccept, e.Payload[0]);
                        Assert.Equal(1UL, BitConverter.ToUInt64(e.Payload, 1));
                        Assert.Contains(1UL, nm.ConnectedClientsIds);
                        return;
                    }
                    else if (e.Kind == NetEventKind.Disconnected) throw new InvalidOperationException("the relay link closed");
                }
                Thread.Sleep(1);
            }
            throw new TimeoutException("no ConnectAccept through the relay");
        }

        // ── Keys ────────────────────────────────────────────────────

        [Fact]
        public async Task AKeyedServer_RefusesTheWrongKey_AndTakesTheRightOne()
        {
            var server = Server(key: "s3cret");
            var stranger = Client("mallory", server, key: "guess", connectWaitMs: 2500);
            var e = await Assert.ThrowsAsync<SessionException>(() => stranger.QuerySessionsAsync(Query("presence")));
            Assert.Contains("No online server", e.Message);
            Assert.Contains("Wrong server key", e.Message);

            var friend = Client("alice", server, key: "s3cret");
            Assert.Empty((await friend.QuerySessionsAsync(Query("presence"))).Sessions);
        }

        // ── LAN discovery and the automatic mode ───────────────────

        [Fact]
        public void Discovery_FindsTheServer_AndOnlyOneServerPerMachineTakesTheLock()
        {
            var server = Server(discovery: true);
            Assert.True(server.DiscoveryActive, $"UDP {OnlineWire.DiscoveryPort} should be free for the test");
            var found = OnlineDiscovery.Probe(600);
            var mine = Assert.Single(found, f => f.Server.Id == server.Id);
            Assert.Equal("127.0.0.1", mine.Address);
            Assert.Equal(server.Port, mine.Server.Port);
            Assert.True(mine.Server.Standalone);

            // The discovery port is the election lock: a game cannot host a second server here.
            Assert.Null(OnlineServer.TryStartEmbedded(null, _ => { }));
        }

        [Fact]
        public async Task AutomaticMode_TheFirstGameHostsTheServer_TheNextFindsIt_AndItIsReplacedWhenThatGameQuits()
        {
            var first = Client("alice", server: null, embed: true);
            WaitFor(() => first.IsConnected, "the first game to host and join its own server");
            Assert.NotNull(first.EmbeddedServer);

            var second = Client("bob", server: null, embed: true);
            WaitFor(() => second.IsConnected, "the second game to find the first one's server");
            Assert.Null(second.EmbeddedServer);
            Assert.Equal(first.ConnectedServer, second.ConnectedServer);

            var hosted = await first.CreateSessionAsync(Options("presence", name: "Alice"));
            var joined = await second.JoinSessionByIdAsync(Assert.Single((await second.QuerySessionsAsync(Query("presence"))).Sessions).Id);
            Assert.Equal(hosted.Id, joined.Id);

            // The hosting game quits: the other one hosts a server itself and brings its session along.
            first.Dispose();
            WaitFor(() => second.EmbeddedServer != null && second.IsConnected, "the second game to take over", 20000);
            await joined.RefreshAsync();
            Assert.Contains(joined.Players, p => p.Id == "bob");
        }
    }
}
