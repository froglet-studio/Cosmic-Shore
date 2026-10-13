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
    /// A stand-in for the UGS Multiplayer service (Lobby + Relay) on ONE machine: sessions live as
    /// JSON files in a directory every player on the machine - or on the LAN, given a shared folder -
    /// can see. Players on different machines use <see cref="OnlineMultiplayerService"/> instead;
    /// this one stays for tests and for <c>COSMIC_SHORE_NET_SERVER=dir</c>. A session carries what the
    /// real one does: id and join code, host, capacity, the indexed session properties queries
    /// filter on, and a roster whose per-player properties the game uses as its invite channel. A
    /// Relay-networked session also records the host's transport endpoint, which is what a join
    /// connects the NetworkManager to.
    ///
    /// Liveness mirrors lobby heartbeats (<see cref="SessionRules"/>): every live session handle
    /// stamps its player every few seconds; a player unseen for <see cref="PlayerTimeout"/> is
    /// dropped from the roster and a session whose host is gone is no longer listed or joinable.
    /// </summary>
    public sealed class DirectoryMultiplayerService : IMultiplayerService
    {
        public static readonly TimeSpan PlayerTimeout = SessionRules.PlayerTimeout;
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

        static string SelfId => Services.AuthenticationService.Instance?.PlayerId is { Length: > 0 } id ? id : "local-player";

        // ── IMultiplayerService ─────────────────────────────────────

        public Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            var nm = NetworkManager.Singleton;
            var rec = SessionRules.NewSession(options, SelfId, SessionRules.NowTicks);
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
                if (SessionRules.Join(r, sessionId, SelfId, SessionRules.ToRecords(options?.PlayerProperties), SessionRules.NowTicks) is { } refused)
                    throw refused.ToException();
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
                long now = SessionRules.NowTicks;
                var result = new List<ISessionInfo>();
                foreach (var file in Directory.EnumerateFiles(_dir, "*.json"))
                {
                    var rec = Load(Path.GetFileNameWithoutExtension(file));
                    if (rec == null) continue;
                    if (!SessionRules.HostAlive(rec, now))
                    {
                        // A session whose host stopped heartbeating long ago is gone (the service expires it).
                        if (SessionRules.Expired(rec, now)) Remove(rec.Id);
                        continue;
                    }
                    if (!SessionRules.Listed(rec, options?.FilterOptions, now)) continue;
                    SessionRules.Prune(rec, now);
                    result.Add(SessionRules.Info(rec));
                }
                return result;
            });
            return Task.FromResult(new QuerySessionsResults(list));
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

        // ── The session handle ─────────────────────────────────────

        sealed class DirectorySession : IHostSession
        {
            readonly DirectoryMultiplayerService _svc;
            readonly bool _networked;
            readonly SessionView _view;
            readonly Timer _heartbeat;
            bool _closed;

            public DirectorySession(DirectoryMultiplayerService svc, SessionRecord rec, bool networked)
            {
                _svc = svc;
                _networked = networked;
                Id = rec.Id;
                Code = rec.Code;
                MaxPlayers = rec.MaxPlayers;
                _view = new SessionView(SelfId, rec.Players.FirstOrDefault(p => p.Id == SelfId)?.Properties);
                Apply(rec);
                _heartbeat = new Timer(_ => Beat(), null, HeartbeatMs, HeartbeatMs);
            }

            public string Id { get; }
            public string Code { get; }
            public bool IsHost => _view.HostId == SelfId;
            public int MaxPlayers { get; }
            public int PlayerCount => _view.Players.Count;
            public Dictionary<string, SessionProperty> Properties => _view.Properties;
            public event Action Deleted;
            public event Action<string> PlayerLeaving;
            public IReadOnlyList<IReadOnlyPlayer> Players => _view.Players;
            public IPlayer CurrentPlayer => _closed ? null : _view.Self;
            public IHostSession AsHost() => this;

            void Apply(SessionRecord rec)
            {
                foreach (var id in _view.Apply(rec)) PlayerLeaving?.Invoke(id);
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
                        me.LastSeen = SessionRules.NowTicks;
                        _svc.Store(rec);
                        return 0;
                    });
                }
                catch (Exception) { }
            }

            public Task RefreshAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                bool stillIn = false;
                var rec = _svc.Locked(() =>
                {
                    var r = _svc.Load(Id);
                    if (r == null) return null;
                    stillIn = SessionRules.Refresh(r, SelfId, SessionRules.NowTicks);
                    _svc.Store(r);
                    return r;
                });
                if (rec == null || !stillIn)
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
                    me.Properties = _view.Self.Snapshot();
                    me.LastSeen = SessionRules.NowTicks;
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
                    if (SessionRules.Leave(r, SelfId)) _svc.Remove(Id);
                    else _svc.Store(r);
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
                _view.Clear();
                // Leaving a networked session stops its NetworkManager (SDK network handler contract).
                if (_networked && NetworkManager.Singleton is { IsListening: true } nm) nm.Shutdown();
            }
        }
    }
}
