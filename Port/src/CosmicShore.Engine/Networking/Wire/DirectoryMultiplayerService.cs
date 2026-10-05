using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// The port's stand-in for the UGS Multiplayer service (Lobby + Relay): sessions live as JSON
    /// files in a directory every player on the machine - or on the LAN, given a shared folder -
    /// can see. A session carries what the real one does: id and join code, host, capacity, the
    /// indexed session properties queries filter on, and a roster whose per-player properties the
    /// game uses as its invite channel. A Relay-networked session also records the host's
    /// transport endpoint, which is what a join connects the NetworkManager to.
    ///
    /// Liveness mirrors lobby heartbeats: every live session handle stamps its player every few
    /// seconds; a player unseen for <see cref="PlayerTimeout"/> is dropped from the roster and a
    /// session whose host is gone is no longer listed or joinable.
    /// </summary>
    public sealed class DirectoryMultiplayerService : IMultiplayerService
    {
        public static readonly TimeSpan PlayerTimeout = TimeSpan.FromSeconds(30);
        const int HeartbeatMs = 4000;

        readonly string _dir;

        public DirectoryMultiplayerService(string directory)
        {
            _dir = directory;
            Directory.CreateDirectory(_dir);
        }

        public static string DefaultDirectory
            => Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_DIR") is { Length: > 0 } d
                ? d
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CosmicShore-sessions");

        // ── Records ─────────────────────────────────────────────────

        internal sealed class PropRecord { public string Value { get; set; } public int Index { get; set; } public int Visibility { get; set; } }

        internal sealed class PlayerRecord
        {
            public string Id { get; set; }
            public Dictionary<string, PropRecord> Properties { get; set; } = new();
            public long LastSeen { get; set; }
        }

        internal sealed class SessionRecord
        {
            public string Id { get; set; }
            public string Code { get; set; }
            public long Created { get; set; }
            public string HostPlayerId { get; set; }
            public int MaxPlayers { get; set; }
            public bool IsLocked { get; set; }
            public bool IsPrivate { get; set; }
            public string RelayAddress { get; set; }
            public int RelayPort { get; set; }
            public Dictionary<string, PropRecord> Properties { get; set; } = new();
            public List<PlayerRecord> Players { get; set; } = new();
        }

        static long NowTicks => DateTime.UtcNow.Ticks;

        string PathOf(string id) => Path.Combine(_dir, id + ".json");

        /// <summary>Read-modify-write under a directory-wide lock (short; retried).</summary>
        internal T Locked<T>(Func<T> body)
        {
            var lockPath = Path.Combine(_dir, ".lock");
            for (int attempt = 0; ; attempt++)
            {
                FileStream fs = null;
                try
                {
                    fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    return body();
                }
                catch (IOException) when (attempt < 200 && fs == null)
                {
                    Thread.Sleep(10);
                }
                finally { fs?.Dispose(); }
            }
        }

        internal SessionRecord Load(string id)
        {
            var p = PathOf(id);
            if (!File.Exists(p)) return null;
            try { return JsonSerializer.Deserialize<SessionRecord>(File.ReadAllText(p)); }
            catch (Exception) { return null; }
        }

        internal void Store(SessionRecord rec)
        {
            var p = PathOf(rec.Id);
            var tmp = p + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(rec));
            File.Move(tmp, p, overwrite: true);
        }

        internal void Remove(string id)
        {
            try { File.Delete(PathOf(id)); } catch (Exception) { }
        }

        static bool Alive(PlayerRecord p) => NowTicks - p.LastSeen < PlayerTimeout.Ticks;

        internal static void Prune(SessionRecord rec) => rec.Players.RemoveAll(p => !Alive(p));

        static bool HostAlive(SessionRecord rec) => rec.Players.Any(p => p.Id == rec.HostPlayerId && Alive(p));

        static Dictionary<string, PropRecord> ToRecords(IDictionary<string, PlayerProperty> props)
        {
            var d = new Dictionary<string, PropRecord>();
            if (props != null)
                foreach (var kv in props) if (kv.Value != null) d[kv.Key] = new PropRecord { Value = kv.Value.Value, Visibility = (int)kv.Value.Visibility };
            return d;
        }

        static string SelfId => Services.AuthenticationService.Instance?.PlayerId is { Length: > 0 } id ? id : "local-player";

        // ── IMultiplayerService ─────────────────────────────────────

        public Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            var nm = NetworkManager.Singleton;
            var rec = new SessionRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Code = NewCode(),
                Created = NowTicks,
                HostPlayerId = SelfId,
                MaxPlayers = options?.MaxPlayers ?? 0,
                IsLocked = options?.IsLocked ?? false,
                IsPrivate = options?.IsPrivate ?? false,
            };
            if (options?.SessionProperties != null)
                foreach (var kv in options.SessionProperties)
                    rec.Properties[kv.Key] = new PropRecord { Value = kv.Value.Value, Index = (int)kv.Value.Index, Visibility = (int)kv.Value.Visibility };
            rec.Players.Add(new PlayerRecord { Id = SelfId, Properties = ToRecords(options?.PlayerProperties), LastSeen = NowTicks });

            if (options?.UseRelay == true && nm != null)
            {
                // The SDK's network handler brings the NetworkManager up as host before the create completes.
                if (!nm.IsListening) nm.StartHost();
                int port = NetDriver.ListenPort;
                if (port > 0) { rec.RelayAddress = AdvertisedAddress(); rec.RelayPort = port; }
            }
            Locked(() => { Store(rec); return 0; });
            return Task.FromResult<ISession>(new DirectorySession(this, rec, options?.UseRelay == true));
        }

        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
        {
            var rec = Locked(() =>
            {
                var r = Load(sessionId);
                if (r == null || !HostAlive(r)) throw new SessionException(SessionError.SessionNotFound, $"Session '{sessionId}' not found.");
                Prune(r);
                var self = r.Players.FirstOrDefault(p => p.Id == SelfId);
                if (self == null)
                {
                    if (r.IsLocked) throw new SessionException(SessionError.Unknown, "Session is locked.");
                    if (r.MaxPlayers > 0 && r.Players.Count >= r.MaxPlayers) throw new SessionException(SessionError.Unknown, "Session is full.");
                    self = new PlayerRecord { Id = SelfId };
                    r.Players.Add(self);
                }
                self.Properties = ToRecords(options?.PlayerProperties);
                self.LastSeen = NowTicks;
                Store(r);
                return r;
            });
            bool networked = rec.RelayPort > 0;
            if (networked && NetworkManager.Singleton is { } nm)
            {
                if (nm.IsListening)
                    Debug.LogWarning("[Multiplayer] Joining a networked session while the NetworkManager is still running; shut it down first.");
                else
                {
                    var t = nm.Transport;
                    if (t != null) t.SetConnectionData(rec.RelayAddress, (ushort)rec.RelayPort);
                    nm.StartClient();
                }
            }
            return Task.FromResult<ISession>(new DirectorySession(this, rec, networked));
        }

        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
        {
            var list = Locked(() =>
            {
                var result = new List<ISessionInfo>();
                foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
                {
                    var rec = Load(Path.GetFileNameWithoutExtension(file));
                    if (rec == null) continue;
                    if (!HostAlive(rec))
                    {
                        // A session whose host stopped heartbeating long ago is gone (the service expires it).
                        if (rec.Players.All(p => NowTicks - p.LastSeen > PlayerTimeout.Ticks * 4)) Remove(rec.Id);
                        continue;
                    }
                    if (rec.IsPrivate || rec.IsLocked) continue;
                    if (options != null && !options.FilterOptions.All(f => Matches(rec, f))) continue;
                    Prune(rec);
                    result.Add(new Info(rec));
                }
                return result;
            });
            return Task.FromResult(new QuerySessionsResults(list));
        }

        static bool Matches(SessionRecord rec, FilterOption f)
        {
            int index = (int)f.Field;
            string value = rec.Properties.Values.FirstOrDefault(p => p.Index == index)?.Value;
            int cmp = string.CompareOrdinal(value ?? "", f.Value ?? "");
            return f.Operation switch
            {
                FilterOperation.Equal => cmp == 0,
                FilterOperation.NotEqual => cmp != 0,
                FilterOperation.Greater => cmp > 0,
                FilterOperation.Less => cmp < 0,
                _ => true,
            };
        }

        static readonly System.Random s_rng = new();
        static string NewCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            lock (s_rng) return new string(Enumerable.Range(0, 6).Select(_ => alphabet[s_rng.Next(alphabet.Length)]).ToArray());
        }

        /// <summary>The address other machines reach this one on: COSMIC_SHORE_NET_ADDRESS, else the first LAN IPv4, else loopback.</summary>
        public static string AdvertisedAddress()
        {
            var env = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_ADDRESS");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            return ua.Address.ToString();
                }
            }
            catch (Exception) { }
            return "127.0.0.1";
        }

        sealed class Info : ISessionInfo
        {
            public Info(SessionRecord r)
            {
                Id = r.Id; Created = new DateTime(r.Created, DateTimeKind.Utc); MaxPlayers = r.MaxPlayers;
                AvailableSlots = r.MaxPlayers > 0 ? Math.Max(0, r.MaxPlayers - r.Players.Count) : int.MaxValue;
                IsLocked = r.IsLocked;
            }
            public string Id { get; }
            public DateTime Created { get; }
            public int MaxPlayers { get; }
            public int AvailableSlots { get; }
            public bool IsLocked { get; }
            public bool HasPassword => false;
        }

        // ── The session handle ─────────────────────────────────────

        sealed class RosterPlayer : IPlayer
        {
            readonly Dictionary<string, PlayerProperty> _props = new();
            public RosterPlayer(string id, Dictionary<string, PropRecord> props)
            {
                Id = id;
                if (props != null)
                    foreach (var kv in props) _props[kv.Key] = new PlayerProperty(kv.Value.Value, (VisibilityPropertyOptions)kv.Value.Visibility);
            }
            public string Id { get; }
            public IReadOnlyDictionary<string, PlayerProperty> Properties => _props;
            public void SetProperty(string key, PlayerProperty property)
            {
                if (property == null) _props.Remove(key);
                else _props[key] = property;
            }
            internal Dictionary<string, PropRecord> Snapshot() => ToRecords(_props);
        }

        sealed class DirectorySession : IHostSession
        {
            readonly DirectoryMultiplayerService _svc;
            readonly bool _networked;
            readonly RosterPlayer _self;
            readonly Timer _heartbeat;
            List<IReadOnlyPlayer> _players = new();
            string _host;
            bool _closed;

            public DirectorySession(DirectoryMultiplayerService svc, SessionRecord rec, bool networked)
            {
                _svc = svc;
                _networked = networked;
                Id = rec.Id;
                Code = rec.Code;
                MaxPlayers = rec.MaxPlayers;
                var mine = rec.Players.FirstOrDefault(p => p.Id == SelfId);
                _self = new RosterPlayer(SelfId, mine?.Properties);
                Apply(rec);
                _heartbeat = new Timer(_ => Beat(), null, HeartbeatMs, HeartbeatMs);
            }

            public string Id { get; }
            public string Code { get; }
            public bool IsHost => _host == SelfId;
            public int MaxPlayers { get; }
            public int PlayerCount => _players.Count;
            public Dictionary<string, SessionProperty> Properties { get; } = new();
            public event Action Deleted;
            public event Action<string> PlayerLeaving;
            public IReadOnlyList<IReadOnlyPlayer> Players => _players;
            public IPlayer CurrentPlayer => _closed ? null : _self;
            public IHostSession AsHost() => this;

            void Apply(SessionRecord rec)
            {
                _host = rec.HostPlayerId;
                var before = new HashSet<string>(_players.Select(p => p.Id));
                var list = new List<IReadOnlyPlayer>();
                foreach (var p in rec.Players)
                    list.Add(p.Id == SelfId ? _self : new RosterPlayer(p.Id, p.Properties));
                _players = list;
                Properties.Clear();
                foreach (var kv in rec.Properties)
                    Properties[kv.Key] = new SessionProperty(kv.Value.Value, (VisibilityPropertyOptions)kv.Value.Visibility, (PropertyIndex)kv.Value.Index);
                foreach (var id in before)
                    if (list.All(p => p.Id != id)) PlayerLeaving?.Invoke(id);
            }

            void Beat()
            {
                if (_closed) return;
                try
                {
                    _svc.Locked(() =>
                    {
                        var rec = _svc.Load(Id);
                        var me = rec?.Players.FirstOrDefault(p => p.Id == SelfId);
                        if (me == null) return 0;
                        me.LastSeen = NowTicks;
                        _svc.Store(rec);
                        return 0;
                    });
                }
                catch (Exception) { }
            }

            public Task RefreshAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                var rec = _svc.Locked(() =>
                {
                    var r = _svc.Load(Id);
                    if (r == null) return null;
                    Prune(r);
                    // The host left without deleting: the service migrates the session to a remaining player.
                    if (r.Players.Count > 0 && r.Players.All(p => p.Id != r.HostPlayerId)) r.HostPlayerId = r.Players[0].Id;
                    var me = r.Players.FirstOrDefault(p => p.Id == SelfId);
                    if (me != null) me.LastSeen = NowTicks;
                    _svc.Store(r);
                    return r;
                });
                if (rec == null || rec.Players.All(p => p.Id != SelfId))
                {
                    CloseLocal();
                    Deleted?.Invoke();
                    throw new SessionException(rec == null ? SessionError.SessionDeleted : SessionError.NotInLobby, $"Session '{Id}' is gone.");
                }
                Apply(rec);
                return Task.CompletedTask;
            }

            public Task SaveCurrentPlayerDataAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                bool ok = _svc.Locked(() =>
                {
                    var r = _svc.Load(Id);
                    var me = r?.Players.FirstOrDefault(p => p.Id == SelfId);
                    if (me == null) return false;
                    me.Properties = _self.Snapshot();
                    me.LastSeen = NowTicks;
                    _svc.Store(r);
                    return true;
                });
                if (!ok) throw new SessionException(SessionError.NotInLobby, $"Session '{Id}' is gone.");
                return Task.CompletedTask;
            }

            public Task LeaveAsync()
            {
                _svc.Locked(() =>
                {
                    var r = _svc.Load(Id);
                    if (r == null) return 0;
                    r.Players.RemoveAll(p => p.Id == SelfId);
                    if (r.Players.Count == 0) _svc.Remove(Id);
                    else
                    {
                        if (r.HostPlayerId == SelfId) r.HostPlayerId = r.Players[0].Id;
                        _svc.Store(r);
                    }
                    return 0;
                });
                Close();
                return Task.CompletedTask;
            }

            public Task DeleteAsync()
            {
                _svc.Locked(() => { _svc.Remove(Id); return 0; });
                Close();
                Deleted?.Invoke();
                return Task.CompletedTask;
            }

            public Task RemovePlayerAsync(string playerId)
            {
                _svc.Locked(() =>
                {
                    var r = _svc.Load(Id);
                    if (r == null) return 0;
                    r.Players.RemoveAll(p => p.Id == playerId);
                    _svc.Store(r);
                    return 0;
                });
                return Task.CompletedTask;
            }

            void CloseLocal()
            {
                if (_closed) return;
                _closed = true;
                _heartbeat.Dispose();
            }

            void Close()
            {
                if (_closed) return;
                CloseLocal();
                _players = new List<IReadOnlyPlayer>();
                // Leaving a networked session stops its NetworkManager (SDK network handler contract).
                if (_networked && NetworkManager.Singleton is { IsListening: true } nm) nm.Shutdown();
            }
        }
    }
}
