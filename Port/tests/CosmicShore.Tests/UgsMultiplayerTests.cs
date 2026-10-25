using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Services;

namespace CosmicShore.Tests
{
    /// <summary>
    /// START ONLINE's sessions (<see cref="UgsMultiplayerService"/>, docs/MULTIPLAYER.md §6.10): the game's
    /// sessions as UGS Lobby lobbies, against <see cref="UgsLobbyStandIn"/>, with each "machine" its own
    /// service and player. Lobby semantics, the errors the game's UgsRequestPolicy reads, the host's
    /// heartbeat, and a match carried over the Unity-Relay-protocol relay through <see cref="NetStack"/>.
    /// </summary>
    [Collection(NetDriverStaticsCollection.Name)]
    public class UgsMultiplayerTests : IDisposable
    {
        readonly UgsLobbyStandIn _lobby = new();
        readonly List<IDisposable> _cleanup = new();
        readonly bool _savedEmulate = NetworkManager.EmulateNetcodeLifecycle;
        readonly bool _savedEnabled = NetDriver.Enabled;
        readonly TimeSpan _savedHeartbeat = UgsMultiplayerService.HeartbeatEvery;

        public UgsMultiplayerTests()
        {
            NetworkManager.Singleton = null;
            RelaySessions.Reset();
        }

        public void Dispose()
        {
            NetDriver.Stop();
            foreach (var d in Enumerable.Reverse(_cleanup)) try { d.Dispose(); } catch (Exception) { }
            _lobby.Dispose();
            NetDriver.TransportFactory = new TcpTransportFactory();
            RelaySessions.Reset();
            MultiplayerService.Instance = null;
            NetworkManager.Singleton = null;
            NetworkManager.EmulateNetcodeLifecycle = _savedEmulate;
            NetDriver.Enabled = _savedEnabled;
            UgsMultiplayerService.HeartbeatEvery = _savedHeartbeat;
        }

        UgsMultiplayerService Player(string id, bool drive = false)
        {
            string token = UgsLobbyStandIn.Token(id);
            var s = new UgsMultiplayerService(new UgsLobbyClient(() => Task.FromResult(token), _lobby.BaseUrl), () => id) { DriveNetworkManager = drive };
            _cleanup.Add(s);
            return s;
        }

        static SessionOptions Options(string mode, int max = 4, string name = null)
        {
            return new SessionOptions
            {
                MaxPlayers = max,
                SessionProperties = new Dictionary<string, SessionProperty> { ["gameMode"] = new(mode, VisibilityPropertyOptions.Public, PropertyIndex.String1) },
                PlayerProperties = name == null ? null : new Dictionary<string, PlayerProperty> { ["displayName"] = new(name, VisibilityPropertyOptions.Member) },
            };
        }

        static QuerySessionsOptions Query(string mode)
        {
            var q = new QuerySessionsOptions();
            q.FilterOptions.Add(new FilterOption(FilterField.StringIndex1, mode, FilterOperation.Equal));
            return q;
        }

        [Fact]
        public async Task ASessionCreatedOnOneMachine_IsFoundJoinedAndSharedFromAnother()
        {
            var alice = Player("alice");
            var bob = Player("bob");

            var hosted = await alice.CreateSessionAsync(Options("presence", name: "Alice"));
            Assert.True(hosted.IsHost);
            Assert.False(string.IsNullOrEmpty(hosted.Code));

            var info = Assert.Single((await bob.QuerySessionsAsync(Query("presence"))).Sessions);
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

            // The invite channel: a per-player property one machine writes and the other reads; a removed one is gone.
            joined.CurrentPlayer.SetProperty("invite_payloads", new PlayerProperty("{\"to\":\"alice\"}", VisibilityPropertyOptions.Member));
            await joined.SaveCurrentPlayerDataAsync();
            await hosted.RefreshAsync();
            var seenBob = hosted.Players.Single(p => p.Id == "bob");
            Assert.Equal("Bob", seenBob.Properties["displayName"].Value);
            Assert.Equal("{\"to\":\"alice\"}", seenBob.Properties["invite_payloads"].Value);
            Assert.Equal("presence", ((UgsMultiplayerService.UgsSession)hosted).Properties["gameMode"].Value);

            joined.CurrentPlayer.SetProperty("invite_payloads", null);
            await joined.SaveCurrentPlayerDataAsync();
            await hosted.RefreshAsync();
            Assert.False(hosted.Players.Single(p => p.Id == "bob").Properties.ContainsKey("invite_payloads"));
        }

        [Fact]
        public async Task Refusals_AreTheSdkErrors_TheGamesRequestPolicyReads()
        {
            var alice = Player("alice");
            var bob = Player("bob");
            var carol = Player("carol");

            var priv = Options("party");
            priv.IsPrivate = true;
            var hidden = await alice.CreateSessionAsync(priv);
            Assert.Empty((await bob.QuerySessionsAsync(Query("party"))).Sessions);
            await bob.JoinSessionByIdAsync(hidden.Id); // private is unlisted, not unjoinable: invites join by id

            var duo = await carol.CreateSessionAsync(Options("duo", max: 2));
            await bob.JoinSessionByIdAsync(duo.Id);
            // UgsRequestPolicy: a full session is SessionError.Unknown with "is full" in its words.
            var full = await Assert.ThrowsAsync<SessionException>(() => alice.JoinSessionByIdAsync(duo.Id));
            Assert.Equal(SessionError.Unknown, full.Error);
            Assert.Contains("is full", full.Message);

            var missing = await Assert.ThrowsAsync<SessionException>(() => alice.JoinSessionByIdAsync("no-such-session"));
            Assert.Equal(SessionError.SessionNotFound, missing.Error);

            var lockedOptions = Options("locked");
            lockedOptions.IsLocked = true;
            var locked = await carol.CreateSessionAsync(lockedOptions);
            var refused = await Assert.ThrowsAsync<SessionException>(() => alice.JoinSessionByIdAsync(locked.Id));
            Assert.Contains("locked", refused.Message);

            _lobby.FailNext(429, "Too Many Requests", "rate limit exceeded");
            var limited = await Assert.ThrowsAsync<SessionException>(() => alice.QuerySessionsAsync(Query("duo")));
            Assert.Equal(SessionError.RateLimitExceeded, limited.Error);

            // 5xx and no answer at all are transient: a RequestFailedException the policy retries.
            _lobby.FailNext(503, "Service Unavailable", "try again");
            var down = await Assert.ThrowsAsync<RequestFailedException>(() => alice.CreateSessionAsync(Options("x")));
            Assert.Equal(503, down.ErrorCode);
            var nobody = new UgsMultiplayerService(new UgsLobbyClient(() => Task.FromResult(UgsLobbyStandIn.Token("dave")), "http://127.0.0.1:9/v1"), () => "dave");
            _cleanup.Add(nobody);
            var unreachable = await Assert.ThrowsAsync<RequestFailedException>(() => nobody.QuerySessionsAsync(Query("x")));
            Assert.Equal(0, unreachable.ErrorCode);
        }

        [Fact]
        public async Task HostLeaving_HandsTheSessionOn_AndMembersHearWhoLeft()
        {
            var alice = Player("alice");
            var bob = Player("bob");
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
        public async Task ARemovedPlayer_AndADeletedSession_CloseTheHandle_AsTheSdkDoes()
        {
            var alice = Player("alice");
            var bob = Player("bob");
            var hosted = await alice.CreateSessionAsync(Options("party"));
            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            int deleted = 0;
            joined.Deleted += () => deleted++;

            await hosted.AsHost().RemovePlayerAsync("bob");
            var kicked = await Assert.ThrowsAsync<SessionException>(() => joined.RefreshAsync());
            Assert.Equal(SessionError.NotInLobby, kicked.Error);
            Assert.Equal(1, deleted);
            Assert.Null(joined.CurrentPlayer);

            var again = await bob.JoinSessionByIdAsync(hosted.Id);
            await hosted.AsHost().DeleteAsync();
            var gone = await Assert.ThrowsAsync<SessionException>(() => again.RefreshAsync());
            Assert.Equal(SessionError.SessionDeleted, gone.Error);
        }

        [Fact]
        public async Task TheHost_HeartbeatsItsLobby_AndAMemberDoesNot()
        {
            UgsMultiplayerService.HeartbeatEvery = TimeSpan.FromMilliseconds(100);
            var alice = Player("alice");
            var bob = Player("bob");
            var hosted = await alice.CreateSessionAsync(Options("party"));
            await bob.JoinSessionByIdAsync(hosted.Id);

            var sw = Stopwatch.StartNew();
            while (_lobby.Heartbeats.GetValueOrDefault(hosted.Id) < 2 && sw.ElapsedMilliseconds < 5000) Thread.Sleep(20);
            Assert.True(_lobby.Heartbeats.GetValueOrDefault(hosted.Id) >= 2, "the host never heartbeat its lobby");
            Assert.Equal(0, _lobby.HeartbeatsRefused); // a member never heartbeats: UGS would refuse it
        }

        [Fact]
        public async Task ARelaySession_PutsTheJoinCodeInTheLobby_AndCarriesAMatch_OverTheUnityRelayProtocolRelay()
        {
            const byte ConnectRequest = 1, ConnectAccept = 2;
            using var relayServer = FrogletRelayServer.Start();
            var loop = new GameLoop(nameof(ARelaySession_PutsTheJoinCodeInTheLobby_AndCarriesAMatch_OverTheUnityRelayProtocolRelay));
            _cleanup.Add(loop);
            NetworkManager.EmulateNetcodeLifecycle = true;
            NetDriver.Enabled = true;

            UgsMultiplayerService alice = null;
            NetStack.Install("udp", () => RelaySessions.Install(new RelayAllocationClient(relayServer.BaseUrl)),
                () => MultiplayerService.Instance = alice = Player("alice", drive: true));
            Assert.Equal("simulated > relay > udp", NetStack.Describe());
            var bob = Player("bob");

            var nm = new GameObject("nm").AddComponent<NetworkManager>();
            NetworkManager.Singleton = nm;
            var hosted = await alice.CreateSessionAsync(Options("match").WithRelayNetwork());
            Assert.True(nm.IsListening);
            Assert.False(string.IsNullOrEmpty(_lobby.AllocationOf(hosted.Id, "alice")));
            // The join code is member-visible lobby data, never a session property the game sees.
            Assert.Equal("member", (string)_lobby.DataOf(hosted.Id)[UgsLobbyMapping.RelayKey]?["visibility"]);
            Assert.False(((UgsMultiplayerService.UgsSession)hosted).Properties.ContainsKey(UgsLobbyMapping.RelayKey));

            var joined = await bob.JoinSessionByIdAsync(hosted.Id);
            var code = ((UgsMultiplayerService.UgsSession)joined).LastRecord.RelayJoinCode;
            Assert.False(string.IsNullOrEmpty(code));

            await RelayedSessions.JoinAsync(code);
            using var client = new RelayTransportFactory(new UdpTransportFactory()).Connect(RelaySessions.RelayAddress, 0, 8000);
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
    }
}
