using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine.Services;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The game's sessions on UGS, as Unity's Multiplayer Services SDK puts them there (docs/MULTIPLAYER.md §6.10):
    /// a session is a UGS Lobby (roster, per-player properties, indexed session properties, host, private/locked,
    /// seat count), and a Relay-networked session's match goes through UGS Relay: the host allocates and starts
    /// before the lobby exists, and the lobby carries the join code a joiner uses. Every call goes to UGS as the
    /// signed-in player, so players on any network meet with no server of our own and no setup.
    ///
    /// A host heartbeats every lobby it holds (UGS marks a lobby inactive after 30 s without one). Lobby refusals
    /// become the SDK's <see cref="SessionException"/> codes the game's <c>UgsRequestPolicy</c> classifies
    /// (429 rate limited, 404 gone, a full lobby by its words, 5xx and no answer as a transient
    /// <see cref="RequestFailedException"/>).
    /// </summary>
    public sealed class UgsMultiplayerService : IMultiplayerService, IDisposable
    {
        readonly UgsLobbyClient _lobby;
        readonly Func<string> _selfId;
        readonly List<UgsSession> _live = new();
        readonly Timer _heartbeat;
        volatile bool _disposed;

        /// <summary>How often a host heartbeats its lobbies (UGS's limit is 30 s).</summary>
        public static TimeSpan HeartbeatEvery { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Start and stop the NetworkManager with Relay-networked sessions, as the SDK's network handler does
        /// (on by default; tests that drive the transport themselves turn it off).
        /// </summary>
        internal bool DriveNetworkManager { get; set; } = true;

        public UgsMultiplayerService(UgsLobbyClient lobby, Func<string> selfId = null)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _selfId = selfId ?? (() => AuthenticationService.Instance?.PlayerId);
            _heartbeat = new Timer(_ => Heartbeat(), null, HeartbeatEvery, HeartbeatEvery);
        }

        string SelfId => _selfId() is { Length: > 0 } id ? id : throw new SessionException(SessionError.NotAuthorized, "Not signed in to UGS.");

        // ── IMultiplayerService ─────────────────────────────────────

        public async Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            options ??= new SessionOptions();
            string self = SelfId;
            var props = Records(options.PlayerProperties);
            var data = UgsLobbyMapping.LobbyData(Records(options.SessionProperties));
            var player = new JsonObject { ["id"] = self, ["data"] = UgsLobbyMapping.PlayerData(props) };

            var nm = NetworkManager.Singleton;
            bool networked = options.UseRelay && DriveNetworkManager && nm != null;
            if (networked && RelayedSessions.Active && await RelayedSessions.HostAsync(nm, options.MaxPlayers) is { } hosted)
            {
                // The host is listening on its allocation already: the lobby names it.
                data[UgsLobbyMapping.RelayKey] = new JsonObject { ["value"] = hosted.JoinCode, ["visibility"] = "member" };
                player["allocationId"] = hosted.AllocationId;
            }
            else if (networked && !nm.IsListening) nm.StartHost(); // no relay configured: a host nobody can reach (one process)

            var body = new JsonObject
            {
                ["name"] = "Cosmic Shore",
                ["maxPlayers"] = options.MaxPlayers > 0 ? options.MaxPlayers : 4,
                ["isPrivate"] = options.IsPrivate,
                ["isLocked"] = options.IsLocked,
                ["player"] = player,
                ["data"] = data,
            };
            JsonNode lobby;
            try { lobby = await _lobby.CreateAsync(body); }
            catch (UgsServiceException e) { throw Fail(e, inSession: false); }
            var session = new UgsSession(this, UgsLobbyMapping.ToRecord(lobby), self, props, options.UseRelay);
            Register(session);
            return session;
        }

        public async Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
        {
            string self = SelfId;
            var props = Records(options?.PlayerProperties);
            JsonNode lobby;
            try { lobby = await _lobby.JoinByIdAsync(sessionId, new JsonObject { ["id"] = self, ["data"] = UgsLobbyMapping.PlayerData(props) }); }
            catch (UgsServiceException e) { throw Fail(e, inSession: false); }
            var rec = UgsLobbyMapping.ToRecord(lobby);
            bool networked = !string.IsNullOrEmpty(rec.RelayJoinCode);
            var session = new UgsSession(this, rec, self, props, networked);
            Register(session);
            if (networked && DriveNetworkManager && NetworkManager.Singleton is { } nm)
            {
                if (nm.IsListening)
                    Debug.LogWarning("[Multiplayer] Joining a networked session while the NetworkManager is still running; shut it down first.");
                else
                {
                    var join = await RelayedSessions.JoinAsync(rec.RelayJoinCode);
                    nm.Transport?.SetConnectionData(RelaySessions.RelayAddress, 0);
                    nm.StartClient();
                    // UGS removes a player whose relay allocation disconnects once the lobby knows it.
                    try { await _lobby.UpdatePlayerAsync(rec.Id, self, new JsonObject { ["allocationId"] = join.AllocationId }); }
                    catch (UgsServiceException) { }
                }
            }
            return session;
        }

        public async Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
        {
            var filters = new JsonArray();
            foreach (var f in options?.FilterOptions ?? new List<FilterOption>())
                filters.Add(new JsonObject { ["field"] = "S" + ((int)f.Field + 1), ["op"] = UgsLobbyMapping.Op(f.Operation), ["value"] = f.Value ?? "" });
            JsonNode reply;
            try { reply = await _lobby.QueryAsync(new JsonObject { ["count"] = 100, ["filter"] = filters }); }
            catch (UgsServiceException e) { throw Fail(e, inSession: false); }
            var list = new List<ISessionInfo>();
            foreach (var l in reply?["results"] as JsonArray ?? new JsonArray())
                if (l != null && !((bool?)l["isLocked"] ?? false)) list.Add(new UgsSessionInfo(l));
            return new QuerySessionsResults(list);
        }

        // ── bookkeeping ─────────────────────────────────────────────

        /// <summary>The last refusal or failure from UGS Lobby, for the diagnostics overlay; null while none.</summary>
        public string LastProblem { get; private set; }

        /// <summary>When the host last heartbeat a lobby successfully (UTC), or null.</summary>
        public DateTime? LastHeartbeat { get; private set; }

        /// <summary>One line per lobby this player is in, for the diagnostics overlay: code, role, seats, relay, properties.</summary>
        public IReadOnlyList<string> DescribeSessions()
        {
            List<UgsSession> live;
            lock (_live) live = _live.ToList();
            return live.Select(s =>
            {
                var line = $"{s.Code ?? s.Id} · {(s.IsHost ? "host" : "member")} · {s.PlayerCount}/{s.MaxPlayers} players";
                if (s.LastRecord?.RelayJoinCode is { Length: > 0 } code) line += " · relay " + code;
                var props = s.Properties.Take(2).Select(kv => $"{kv.Key}={kv.Value.Value}").ToList();
                if (props.Count > 0) line += " · " + string.Join(", ", props);
                return line;
            }).ToList();
        }

        internal Exception Fail(UgsServiceException e, bool inSession)
        {
            LastProblem = e.Message;
            return UgsLobbyMapping.Map(e, inSession);
        }

        internal UgsLobbyClient Lobby => _lobby;

        void Register(UgsSession s) { lock (_live) _live.Add(s); }
        internal void Unregister(UgsSession s) { lock (_live) _live.Remove(s); }

        void Heartbeat()
        {
            if (_disposed) return;
            List<UgsSession> hosted;
            lock (_live) hosted = _live.Where(s => s.IsHost).ToList();
            foreach (var s in hosted)
                try { _lobby.HeartbeatAsync(s.Id).GetAwaiter().GetResult(); LastHeartbeat = DateTime.UtcNow; }
                catch (Exception e) { LastProblem = "heartbeat: " + e.Message; } // the next refresh reports a lobby that is gone
        }

        static Dictionary<string, PropRecord> Records(Dictionary<string, PlayerProperty> props) => SessionRules.ToRecords(props);

        static Dictionary<string, PropRecord> Records(Dictionary<string, SessionProperty> props)
        {
            var d = new Dictionary<string, PropRecord>();
            if (props != null)
                foreach (var kv in props)
                    d[kv.Key] = new PropRecord { Value = kv.Value.Value, Index = (int)kv.Value.Index, Visibility = (int)kv.Value.Visibility };
            return d;
        }

        public void Dispose()
        {
            _disposed = true;
            _heartbeat.Dispose();
        }

        /// <summary>A UGS lobby as the game's session handle.</summary>
        internal sealed class UgsSession : IHostSession
        {
            readonly UgsMultiplayerService _svc;
            readonly SessionView _view;
            readonly string _self;
            readonly bool _networked;
            HashSet<string> _savedKeys;
            bool _closed;

            public UgsSession(UgsMultiplayerService svc, SessionRecord rec, string self, Dictionary<string, PropRecord> props, bool networked)
            {
                _svc = svc;
                _self = self;
                _networked = networked;
                Id = rec.Id;
                Code = rec.Code;
                MaxPlayers = rec.MaxPlayers;
                _savedKeys = new HashSet<string>(props.Keys);
                _view = new SessionView(self, rec.Players.FirstOrDefault(p => p.Id == self)?.Properties ?? props);
                Apply(rec);
            }

            internal SessionRecord LastRecord { get; private set; }

            public string Id { get; }
            public string Code { get; }
            public bool IsHost => !_closed && _view.HostId == _self;
            public int MaxPlayers { get; }
            public int PlayerCount => _view.Players.Count;
            public Dictionary<string, SessionProperty> Properties => _view.Properties;
            public event Action Deleted;
            public event Action<string> PlayerLeaving;
            public event Action<string> PlayerJoined;
            public event Action<string> PlayerHasLeft;
            public event Action PlayerPropertiesChanged;
            public IReadOnlyList<IReadOnlyPlayer> Players => _view.Players;
            public IPlayer CurrentPlayer => _closed ? null : _view.Self;
            public IHostSession AsHost() => this;

            void Apply(SessionRecord rec)
            {
                LastRecord = rec;
                var before = new HashSet<string>(_view.Players.Select(p => p.Id));
                var left = _view.Apply(rec).ToList();
                foreach (var id in left) PlayerLeaving?.Invoke(id);
                // The SDK's order: PlayerLeaving before the roster drops a player, PlayerHasLeft after.
                // This engine applies a whole record at once, so both follow the one Apply.
                foreach (var id in left) PlayerHasLeft?.Invoke(id);
                foreach (var p in _view.Players)
                    if (!before.Contains(p.Id)) PlayerJoined?.Invoke(p.Id);
            }

            SessionException Gone(SessionError error, string message)
            {
                CloseLocal();
                Deleted?.Invoke();
                return new SessionException(error, message);
            }

            public async Task RefreshAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                JsonNode lobby;
                try { lobby = await _svc.Lobby.GetAsync(Id); }
                catch (UgsServiceException e) when (e.Status is 404) { throw Gone(SessionError.SessionDeleted, $"Session '{Id}' is gone: {e.Message}"); }
                catch (UgsServiceException e) when (e.Status is 403) { throw Gone(SessionError.NotInLobby, $"Not in session '{Id}': {e.Message}"); }
                catch (UgsServiceException e) { throw _svc.Fail(e, inSession: true); }
                if (_closed) return;
                var rec = UgsLobbyMapping.ToRecord(lobby);
                // Removed by the host: the lobby no longer lists this player.
                if (rec.Players.All(p => p.Id != _self)) throw Gone(SessionError.NotInLobby, $"Not in session '{Id}' any more.");
                Apply(rec);
            }

            public async Task SaveCurrentPlayerDataAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                var props = _view.Self.Snapshot();
                var data = UgsLobbyMapping.PlayerData(props);
                foreach (var gone in _savedKeys.Where(k => !props.ContainsKey(k))) data[gone] = null; // UGS removes a key sent as null
                JsonNode lobby;
                try { lobby = await _svc.Lobby.UpdatePlayerAsync(Id, _self, new JsonObject { ["data"] = data }); }
                catch (UgsServiceException e) { throw _svc.Fail(e, inSession: true); }
                _savedKeys = new HashSet<string>(props.Keys);
                if (lobby != null && !_closed) Apply(UgsLobbyMapping.ToRecord(lobby));
            }

            /// <summary>Best effort: the handle closes even when UGS cannot be told.</summary>
            public async Task LeaveAsync()
            {
                if (!_closed)
                    try { await _svc.Lobby.RemovePlayerAsync(Id, _self); }
                    catch (UgsServiceException) { }
                Close();
            }

            public async Task DeleteAsync()
            {
                if (!_closed)
                    try { await _svc.Lobby.DeleteAsync(Id); }
                    catch (UgsServiceException) { }
                Close();
                Deleted?.Invoke();
            }

            public async Task RemovePlayerAsync(string playerId)
            {
                try { await _svc.Lobby.RemovePlayerAsync(Id, playerId); }
                catch (UgsServiceException e) { throw _svc.Fail(e, inSession: true); }
            }

            void CloseLocal()
            {
                if (_closed) return;
                _closed = true;
                _svc.Unregister(this);
            }

            void Close()
            {
                if (_closed) return;
                CloseLocal();
                _view.Clear();
                // Leaving a networked session stops its NetworkManager (SDK network handler contract).
                if (_networked && _svc.DriveNetworkManager && NetworkManager.Singleton is { IsListening: true } nm) nm.Shutdown();
            }
        }
    }

    /// <summary>A UGS lobby as a query result.</summary>
    internal sealed class UgsSessionInfo : ISessionInfo
    {
        public UgsSessionInfo(JsonNode l)
        {
            Id = (string)l["id"];
            Created = UgsLobbyMapping.Time(l["created"]);
            MaxPlayers = (int?)l["maxPlayers"] ?? 0;
            AvailableSlots = (int?)l["availableSlots"] ?? Math.Max(0, MaxPlayers - ((l["players"] as JsonArray)?.Count ?? 0));
            IsLocked = (bool?)l["isLocked"] ?? false;
            HasPassword = (bool?)l["hasPassword"] ?? false;
        }

        public string Id { get; }
        public DateTime Created { get; }
        public int MaxPlayers { get; }
        public int AvailableSlots { get; }
        public bool IsLocked { get; }
        public bool HasPassword { get; }
    }

    /// <summary>Between the game's session shapes and UGS Lobby's JSON.</summary>
    internal static class UgsLobbyMapping
    {
        /// <summary>The lobby data key holding the host's UGS Relay join code (member-visible, as the SDK keeps it).</summary>
        public const string RelayKey = "amoebius_relay";

        static readonly string[] Visibility = { "public", "member", "private" };

        static string Vis(int v) => v >= 0 && v < Visibility.Length ? Visibility[v] : "public";

        static int Vis(string v) => Array.FindIndex(Visibility, x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : 0;

        /// <summary>Session properties as lobby data: each with its visibility and query index (S1..S5).</summary>
        public static JsonObject LobbyData(Dictionary<string, PropRecord> props)
        {
            var o = new JsonObject();
            foreach (var kv in props)
                o[kv.Key] = new JsonObject { ["value"] = kv.Value.Value, ["visibility"] = Vis(kv.Value.Visibility), ["index"] = "S" + (kv.Value.Index + 1) };
            return o;
        }

        /// <summary>Player properties as player data (UGS allows no index on player data).</summary>
        public static JsonObject PlayerData(Dictionary<string, PropRecord> props)
        {
            var o = new JsonObject();
            foreach (var kv in props ?? new Dictionary<string, PropRecord>())
                o[kv.Key] = new JsonObject { ["value"] = kv.Value.Value, ["visibility"] = Vis(kv.Value.Visibility) };
            return o;
        }

        static Dictionary<string, PropRecord> Props(JsonNode data, bool lobbyData)
        {
            var d = new Dictionary<string, PropRecord>();
            if (data is not JsonObject o) return d;
            foreach (var kv in o)
            {
                if (kv.Value is null || (lobbyData && kv.Key == RelayKey)) continue;
                int index = 0;
                if ((string)kv.Value["index"] is { Length: > 1 } idx && idx[0] == 'S' && int.TryParse(idx[1..], out var n)) index = Math.Clamp(n - 1, 0, 4);
                d[kv.Key] = new PropRecord { Value = (string)kv.Value["value"], Visibility = Vis((string)kv.Value["visibility"]), Index = index };
            }
            return d;
        }

        public static SessionRecord ToRecord(JsonNode l)
        {
            if (l == null) throw new SessionException(SessionError.Unknown, "UGS Lobby returned no lobby.");
            var rec = new SessionRecord
            {
                Id = (string)l["id"],
                Code = (string)l["lobbyCode"],
                HostPlayerId = (string)l["hostId"],
                MaxPlayers = (int?)l["maxPlayers"] ?? 0,
                IsPrivate = (bool?)l["isPrivate"] ?? false,
                IsLocked = (bool?)l["isLocked"] ?? false,
                Created = Time(l["created"]).Ticks,
                Properties = Props(l["data"], lobbyData: true),
                RelayJoinCode = (string)l["data"]?[RelayKey]?["value"],
            };
            foreach (var p in l["players"] as JsonArray ?? new JsonArray())
                if ((string)p?["id"] is { Length: > 0 } id)
                    rec.Players.Add(new PlayerRecord { Id = id, Properties = Props(p["data"], lobbyData: false) });
            return rec;
        }

        public static DateTime Time(JsonNode n) =>
            n is not null && DateTime.TryParse((string)n, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t)
                ? t : DateTime.UtcNow;

        public static string Op(FilterOperation op) => op switch
        {
            FilterOperation.Equal => "EQ",
            FilterOperation.NotEqual => "NE",
            FilterOperation.Greater => "GT",
            FilterOperation.Less => "LT",
            _ => "EQ",
        };

        /// <summary>
        /// A Lobby refusal as the SDK's error: the game's <c>UgsRequestPolicy</c> reads the code first, then the
        /// words (a full lobby is "is full"); a missing answer or a 5xx is a transient <see cref="RequestFailedException"/>.
        /// </summary>
        public static Exception Map(UgsServiceException e, bool inSession)
        {
            string m = e.Message;
            bool Says(string word) => m.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
            if (Says("is full") || Says("LobbyFull") || Says("lobby full")) return new SessionException(SessionError.Unknown, "Session is full. " + m, e);
            switch (e.Status)
            {
                case 429: return new SessionException(SessionError.RateLimitExceeded, m, e);
                case 404: return new SessionException(inSession ? SessionError.SessionDeleted : SessionError.SessionNotFound, "Session not found: " + m, e);
                case 403 when Says("locked"): return new SessionException(SessionError.Unknown, "Session is locked. " + m, e);
                case 403: return new SessionException(inSession ? SessionError.NotInLobby : SessionError.Forbidden, m, e);
                case 401: return new SessionException(SessionError.NotAuthorized, m, e);
                case 409: return new SessionException(SessionError.SessionConflict, m, e);
                case 400: return new SessionException(SessionError.InvalidParameter, m, e);
                default: return new RequestFailedException(e.Status, m, e);
            }
        }
    }
}
