using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>How a game reaches the online server.</summary>
    public sealed class OnlineClientOptions
    {
        /// <summary>
        /// "host[:port]" of an <see cref="OnlineServer"/> (internet play), or empty/"auto": find one on
        /// the LAN, else host one inside this game for the network.
        /// </summary>
        public string Server { get; set; }
        /// <summary>The server's key, when it has one.</summary>
        public string Key { get; set; }
        /// <summary>Automatic mode may host a server inside this game when the LAN has none.</summary>
        public bool AllowEmbeddedServer { get; set; } = true;
        /// <summary>Shown in the server's log (the commit a game was built from).</summary>
        public string Build { get; set; }
        /// <summary>This player's id; defaults to the signed-in identity (the install's anonymous id).</summary>
        public Func<string> PlayerId { get; set; }
        public Action<string> Log { get; set; }
        public int DiscoveryMs { get; set; } = 900;
        /// <summary>How long a lobby call waits for a connection before it fails.</summary>
        public int ConnectWaitMs { get; set; } = 10000;
        public int RequestTimeoutMs { get; set; } = 10000;

        public bool Automatic => string.IsNullOrWhiteSpace(Server) || Server.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// COSMIC_SHORE_NET_SERVER (empty or "auto": automatic; "host[:port]": that server),
        /// COSMIC_SHORE_NET_KEY, COSMIC_SHORE_NET_EMBED=0 (never host a server in the game),
        /// COSMIC_SHORE_COMMIT (the build shown in the server's log).
        /// </summary>
        public static OnlineClientOptions FromEnvironment() => new()
        {
            Server = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_SERVER"),
            Key = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_KEY"),
            AllowEmbeddedServer = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_EMBED") != "0",
            Build = Environment.GetEnvironmentVariable("COSMIC_SHORE_COMMIT"),
        };
    }

    /// <summary>
    /// The UGS Multiplayer service (Lobby + Relay) for players on different machines: every lobby
    /// call is a request to an <see cref="OnlineServer"/> over one control connection, and a join to
    /// a Relay-networked session connects the NetworkManager through the server's relay
    /// (<see cref="OnlineTransportFactory"/>), so the host needs no reachable address. Same
    /// semantics as the single-machine <see cref="DirectoryMultiplayerService"/>: both apply
    /// <see cref="SessionRules"/>.
    ///
    /// The connection is kept up in the background: it finds the server (a fixed address, or LAN
    /// discovery that falls back to hosting one in this game), reconnects with backoff when it
    /// drops, follows a server that stood down for another, and on every (re)connect re-asserts the
    /// sessions this game holds, so a server restart or a move loses no party. Lobby calls made
    /// while it is down wait for it (<see cref="OnlineClientOptions.ConnectWaitMs"/>), then fail
    /// with a <see cref="SessionException"/> the game's own retry and offline paths handle.
    /// </summary>
    public sealed class OnlineMultiplayerService : IMultiplayerService, IDisposable
    {
        const int PingMs = 5000;

        readonly OnlineClientOptions _o;
        readonly object _gate = new();
        readonly ConcurrentDictionary<long, TaskCompletionSource<OnlineMessage>> _pending = new();
        readonly List<OnlineSession> _live = new();
        readonly ManualResetEventSlim _wake = new(false);
        readonly object _sendLock = new();
        Thread _connector;
        long _nextRid;

        TcpClient _tcp;
        NetworkStream _stream;
        string _host, _connectedAs, _lastError;
        int _port;
        TaskCompletionSource<bool> _up = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool _ready;
        sealed record Endpoint(string Host, int Port);
        volatile Endpoint _movedTo;
        volatile OnlineServer _embedded;
        NetSocket _hostTransport;
        volatile bool _disposed;

        public OnlineMultiplayerService(OnlineClientOptions options) => _o = options ?? new OnlineClientOptions();

        /// <summary>
        /// Start and stop the NetworkManager with Relay-networked sessions, as the SDK's network
        /// handler does (on by default; tests that drive the transport themselves turn it off).
        /// </summary>
        internal bool DriveNetworkManager { get; set; } = true;

        /// <summary>
        /// Makes this the game's multiplayer service and its netcode transport, and starts connecting.
        /// Call once at boot, with networking enabled.
        /// </summary>
        public static OnlineMultiplayerService Install(OnlineClientOptions options)
        {
            var svc = new OnlineMultiplayerService(options);
            MultiplayerService.Instance = svc;
            // Over the transport already chosen (UDP or TCP, and the configured relay): matches whose host
            // allocated on that relay use it; the rest go through this server's own relay.
            NetDriver.TransportFactory = new OnlineTransportFactory(svc, NetDriver.TransportFactory);
            svc.Start();
            return svc;
        }

        public void Start()
        {
            if (_connector != null) return;
            _connector = new Thread(ConnectorLoop) { IsBackground = true, Name = "online-connector" };
            _connector.Start();
        }

        void Log(string m) => (_o.Log ?? (x => Console.WriteLine("[online] " + x)))(m);

        string SelfId
        {
            get
            {
                var id = _o.PlayerId?.Invoke();
                if (string.IsNullOrEmpty(id)) id = Services.AuthenticationService.Instance?.PlayerId;
                if (string.IsNullOrEmpty(id)) id = Services.AuthenticationService.AnonymousIdProvider?.Invoke();
                return string.IsNullOrEmpty(id) ? "local-player" : id;
            }
        }

        /// <summary>Connected, and every session this game holds is back on the server: lobby calls go straight through.</summary>
        public bool IsConnected
        {
            get { lock (_gate) return _ready; }
        }

        bool HasConnection
        {
            get { lock (_gate) return _tcp != null; }
        }

        /// <summary>The server this game is connected to ("host:port"), or null.</summary>
        public string ConnectedServer
        {
            get { lock (_gate) return _tcp != null ? $"{_host}:{_port}" : null; }
        }

        /// <summary>The server hosted inside this game (automatic mode, when the LAN had none), or null.</summary>
        public OnlineServer EmbeddedServer => _embedded;

        // ── Connection ──────────────────────────────────────────────

        void ConnectorLoop()
        {
            int backoff = 250;
            var rng = new System.Random();
            while (!_disposed)
            {
                if (HasConnection)
                {
                    _wake.Wait(PingMs);
                    _wake.Reset();
                    if (_disposed) break;
                    RetireStoodDownServer();
                    if (HasConnection && _connectedAs != SelfId)
                    {
                        Log($"player id changed to {SelfId}: reconnecting");
                        Drop();
                    }
                    else if (HasConnection) KeepAlive();
                    continue;
                }
                try
                {
                    ConnectOnce();
                    backoff = 250;
                    // The game's own requests wait until every session it holds is back on the server:
                    // a refresh that beat its re-assert would hear "deleted" and tear a live party down.
                    Reassert();
                    lock (_gate)
                        if (_tcp != null)
                        {
                            _ready = true;
                            _up.TrySetResult(true);
                        }
                }
                catch (Exception e)
                {
                    var why = e.GetBaseException().Message;
                    if (why != _lastError) Log($"no online server yet: {why}");
                    _lastError = why;
                    _wake.Wait(backoff + rng.Next(0, 250));
                    _wake.Reset();
                    backoff = Math.Min(backoff * 2, 5000);
                }
            }
        }

        void KeepAlive()
        {
            try { RequestSync(new OnlineMessage { Op = "ping" }); }
            catch (Exception) { Drop(); }
        }

        /// <summary>A stood-down embedded server is disposed once its last relay link closes.</summary>
        void RetireStoodDownServer()
        {
            var e = _embedded;
            if (e != null && e.Yielded && e.RelayLinks == 0)
            {
                e.Dispose();
                if (ReferenceEquals(_embedded, e)) _embedded = null;
            }
        }

        IEnumerable<(string host, int port, string label)> Candidates()
        {
            // Where the last server sent everyone: preferred until a connection to it fails.
            if (_movedTo is { } moved) yield return (moved.Host, moved.Port, "the server everyone moved to");
            if (!_o.Automatic)
            {
                if (!OnlineWire.TryParseEndpoint(_o.Server, out var host, out var port))
                    throw new FormatException($"COSMIC_SHORE_NET_SERVER '{_o.Server}' is not host[:port]");
                yield return (host, port, "the online server");
                yield break;
            }
            var found = OnlineDiscovery.Probe(_o.DiscoveryMs);
            foreach (var f in found)
                yield return (f.Address, f.Server.Port, f.Address == "127.0.0.1" ? $"the server on this machine ({f.Server.Name})" : $"the LAN server on {f.Server.Name}");
            if (found.Count > 0 || !_o.AllowEmbeddedServer) yield break;

            // Nobody on the network runs a server: this game hosts one (the discovery port is the
            // election lock - if another game on this machine just took it, connect to that one).
            var mine = _embedded;
            if (mine == null || mine.Yielded)
            {
                mine = OnlineServer.TryStartEmbedded(_o.Key, Log);
                if (mine != null)
                {
                    _embedded = mine;
                    mine.StoodDown += (addr, port) => { _movedTo = new Endpoint(addr, port); };
                }
            }
            if (mine != null) yield return ("127.0.0.1", mine.Port, "this game's server");
            else
                foreach (var f in OnlineDiscovery.Probe(_o.DiscoveryMs * 2, new[] { System.Net.IPAddress.Loopback }))
                    yield return (f.Address, f.Server.Port, $"the server on {f.Server.Name}");
        }

        void ConnectOnce()
        {
            Exception last = null;
            foreach (var (host, port, label) in Candidates())
            {
                if (_disposed) return;
                try
                {
                    Open(host, port, label);
                    return;
                }
                catch (SessionException e) { last = e; Log($"{host}:{port} refused: {e.Message}"); }
                catch (Exception e) { last = e; }
                if (_movedTo is { } moved && moved.Host == host && moved.Port == port) _movedTo = null;
            }
            throw last ?? new IOException(_o.Automatic ? "none found on the network" : "unreachable");
        }

        void Open(string host, int port, string label)
        {
            var self = SelfId;
            var tcp = OnlineWire.Connect(host, port, 3000);
            try
            {
                var s = tcp.GetStream();
                tcp.ReceiveTimeout = 5000;
                OnlineWire.WriteFrame(s, new OnlineMessage { Op = "hello", V = OnlineWire.Version, Player = self, Key = _o.Key, Build = _o.Build });
                var reply = OnlineWire.ReadFrame(s);
                if (!reply.Ok) throw new SessionException(ParseError(reply.Err), reply.Msg ?? "refused");
                tcp.ReceiveTimeout = 0;
                lock (_gate)
                {
                    _tcp = tcp;
                    _stream = s;
                    _host = host;
                    _port = port;
                    _connectedAs = self;
                }
                _lastError = null;
                new Thread(() => ReadLoop(tcp, s)) { IsBackground = true, Name = "online-read" }.Start();
                Log($"connected to {label} at {host}:{port} as {self}");
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        void ReadLoop(TcpClient tcp, NetworkStream s)
        {
            try
            {
                while (!_disposed)
                {
                    var m = OnlineWire.ReadFrame(s);
                    if (!string.IsNullOrEmpty(m.Ev)) OnEvent(m);
                    else if (_pending.TryRemove(m.Rid, out var tcs)) tcs.TrySetResult(m);
                }
            }
            catch (Exception) { }
            Lost(tcp);
        }

        void OnEvent(OnlineMessage m)
        {
            switch (m.Ev)
            {
                case "link":
                    ThreadPool.QueueUserWorkItem(_ => AcceptRelayLink(m.Sid, m.Link));
                    break;
                case "moved":
                    Log($"the server stood down for {m.Addr}:{m.Port}: moving there");
                    _movedTo = new Endpoint(m.Addr, m.Port);
                    Drop();
                    break;
            }
        }

        /// <summary>Closes the connection (the connector reconnects).</summary>
        void Drop()
        {
            TcpClient tcp;
            lock (_gate) tcp = _tcp;
            if (tcp != null) Lost(tcp);
        }

        void Lost(TcpClient tcp)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_tcp, tcp)) return;
                _tcp = null;
                _stream = null;
                _ready = false;
                _up = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            try { tcp.Close(); } catch { }
            foreach (var rid in _pending.Keys.ToList())
                if (_pending.TryRemove(rid, out var tcs))
                    tcs.TrySetException(new SessionException(SessionError.Unknown, "Lost the connection to the online server."));
            if (!_disposed) Log("disconnected from the online server; reconnecting");
            _wake.Set();
        }

        // ── Requests ────────────────────────────────────────────────

        static SessionError ParseError(string err) => Enum.TryParse<SessionError>(err, out var e) ? e : SessionError.Unknown;

        async Task EnsureConnectedAsync()
        {
            Task up;
            lock (_gate)
            {
                if (_ready) return;
                up = _up.Task;
            }
            _wake.Set();
            if (await Task.WhenAny(up, Task.Delay(_o.ConnectWaitMs)) != up)
                throw new SessionException(SessionError.Unknown,
                    $"No online server{(_o.Automatic ? " on the network" : $" at {_o.Server}")}{(_lastError != null ? $" ({_lastError})" : "")}.");
        }

        TaskCompletionSource<OnlineMessage> Send(OnlineMessage m)
        {
            NetworkStream s;
            TcpClient tcp;
            lock (_gate) { s = _stream; tcp = _tcp; }
            if (s == null) throw new SessionException(SessionError.Unknown, "Not connected to the online server.");
            m.Rid = Interlocked.Increment(ref _nextRid);
            var tcs = new TaskCompletionSource<OnlineMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[m.Rid] = tcs;
            try
            {
                lock (_sendLock) OnlineWire.WriteFrame(s, m);
            }
            catch (Exception e)
            {
                _pending.TryRemove(m.Rid, out _);
                Lost(tcp);
                throw new SessionException(SessionError.Unknown, "Lost the connection to the online server.", e);
            }
            return tcs;
        }

        /// <summary>A message the server does not answer (best effort).</summary>
        void Notify(OnlineMessage m)
        {
            NetworkStream s;
            lock (_gate) s = _stream;
            if (s == null) return;
            try { lock (_sendLock) OnlineWire.WriteFrame(s, m); }
            catch (Exception) { }
        }

        /// <summary>One lobby request: waits for a connection, sends, and turns a refusal into the SDK's exception.</summary>
        internal async Task<OnlineMessage> Request(OnlineMessage m)
        {
            await EnsureConnectedAsync();
            var tcs = Send(m);
            var done = await Task.WhenAny(tcs.Task, Task.Delay(_o.RequestTimeoutMs));
            if (done != tcs.Task)
            {
                _pending.TryRemove(m.Rid, out _);
                throw new SessionException(SessionError.Unknown, "The online server did not answer.");
            }
            var reply = await tcs.Task;
            if (!reply.Ok) throw new SessionException(ParseError(reply.Err), reply.Msg ?? reply.Err ?? "refused");
            return reply;
        }

        /// <summary>A request from a background thread (the connector), blocking it.</summary>
        OnlineMessage RequestSync(OnlineMessage m)
        {
            var tcs = Send(m);
            if (!tcs.Task.Wait(_o.RequestTimeoutMs))
            {
                _pending.TryRemove(m.Rid, out _);
                throw new SessionException(SessionError.Unknown, "The online server did not answer.");
            }
            var reply = tcs.Task.Result;
            if (!reply.Ok) throw new SessionException(ParseError(reply.Err), reply.Msg ?? "refused");
            return reply;
        }

        /// <summary>After a (re)connect: every session this game holds is asserted on the server.</summary>
        void Reassert()
        {
            List<OnlineSession> live;
            lock (_live) live = _live.ToList();
            foreach (var s in live)
            {
                if (s.LastRecord == null) continue;
                try { RequestSync(new OnlineMessage { Op = "assert", Session = s.LastRecord, Props = s.SavedProps }); }
                catch (SessionException e) when (e.Error is SessionError.SessionDeleted or SessionError.NotInLobby or SessionError.SessionNotFound)
                {
                    s.MarkGone(e.Error, e.Message);
                }
                catch (Exception e)
                {
                    Log($"could not re-assert session {s.Id}: {e.GetBaseException().Message}");
                    return;
                }
            }
            if (live.Count > 0) Log($"re-asserted {live.Count} session(s)");
        }

        internal void Register(OnlineSession s) { lock (_live) _live.Add(s); }
        internal void Unregister(OnlineSession s) { lock (_live) _live.Remove(s); }

        // ── IMultiplayerService ─────────────────────────────────────

        public async Task<ISession> CreateSessionAsync(SessionOptions options)
        {
            string self = SelfId;
            var rec = SessionRules.NewSession(options, self, SessionRules.NowTicks);
            rec.Relay = options?.UseRelay == true;
            var props = SessionRules.ToRecords(options?.PlayerProperties);
            // A configured Unity-Relay-protocol relay (COSMIC_SHORE_RELAY: UGS Relay or Froglet's relay server)
            // carries the match over UDP: the host allocates and starts first, and the record carries the join code.
            bool viaRelay = rec.Relay && DriveNetworkManager && RelayedSessions.Active && NetworkManager.Singleton != null;
            if (viaRelay && await RelayedSessions.HostAsync(NetworkManager.Singleton, rec.MaxPlayers) is { } code)
            {
                rec.RelayJoinCode = code;
                rec.RelayAddress = RelaySessions.RelayAddress;
            }
            var reply = await Request(new OnlineMessage { Op = "create", Session = rec });
            var session = new OnlineSession(this, reply.Session, self, props);
            Register(session);
            // The SDK's network handler brings the NetworkManager up as host before the create completes.
            if (rec.Relay && !viaRelay && DriveNetworkManager && NetworkManager.Singleton is { } nm && !nm.IsListening) nm.StartHost();
            return session;
        }

        public async Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions options = null)
        {
            string self = SelfId;
            var props = SessionRules.ToRecords(options?.PlayerProperties);
            var reply = await Request(new OnlineMessage { Op = "join", Sid = sessionId, Props = props });
            var rec = reply.Session;
            var session = new OnlineSession(this, rec, self, props);
            Register(session);
            if (rec.Relay && DriveNetworkManager && NetworkManager.Singleton is { } nm)
            {
                if (nm.IsListening)
                    Debug.LogWarning("[Multiplayer] Joining a networked session while the NetworkManager is still running; shut it down first.");
                else if (!string.IsNullOrEmpty(rec.RelayJoinCode))
                {
                    // The host is on the configured relay: join it by code, over UDP.
                    await RelayedSessions.JoinAsync(rec.RelayJoinCode);
                    nm.Transport?.SetConnectionData(RelaySessions.RelayAddress, 0);
                    nm.StartClient();
                }
                else
                {
                    // The transport connects through the server's relay to the session's host.
                    nm.Transport?.SetConnectionData(OnlineWire.RelayScheme + rec.Id, 0);
                    nm.StartClient();
                }
            }
            return session;
        }

        public async Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions options)
        {
            var reply = await Request(new OnlineMessage { Op = "query", Filters = options?.FilterOptions.Select(FilterRecord.From).ToList() });
            return new QuerySessionsResults(reply.Found?.Cast<ISessionInfo>().ToList() ?? new List<ISessionInfo>());
        }

        // ── Relay links ─────────────────────────────────────────────

        /// <summary>The host's listening transport (from <see cref="OnlineTransportFactory.Listen"/>): relayed clients join it.</summary>
        internal void AttachHost(NetSocket host) { lock (_gate) _hostTransport = host; }

        /// <summary>A joining client's relay link: a socket that, once the host accepted, carries the netcode to it.</summary>
        internal TcpClient OpenRelayLink(string sessionId, int timeoutMs)
        {
            string host;
            int port;
            lock (_gate) { host = _host; port = _port; }
            if (host == null) throw new IOException("not connected to the online server");
            var tcp = OnlineWire.Connect(host, port, Math.Min(timeoutMs, 5000));
            try
            {
                var s = tcp.GetStream();
                tcp.ReceiveTimeout = timeoutMs;
                OnlineWire.WriteFrame(s, new OnlineMessage { Op = "link-open", V = OnlineWire.Version, Sid = sessionId, Player = SelfId, Key = _o.Key });
                var reply = OnlineWire.ReadFrame(s);
                if (!reply.Ok) throw new IOException(reply.Msg ?? "the relay refused the connection");
                tcp.ReceiveTimeout = 0;
                return tcp;
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        /// <summary>The server asks for the host's half of a relay link: open it and hand it to the listening transport.</summary>
        void AcceptRelayLink(string sessionId, string linkId)
        {
            NetSocket hostSocket;
            string host;
            int port;
            lock (_gate) { hostSocket = _hostTransport; host = _host; port = _port; }
            if (hostSocket == null || !hostSocket.IsListening || host == null)
            {
                // Not hosting (the NetworkManager stopped): the joining client hears no at once.
                Notify(new OnlineMessage { Op = "link-reject", Link = linkId });
                return;
            }
            TcpClient tcp = null;
            try
            {
                tcp = OnlineWire.Connect(host, port, 5000);
                var s = tcp.GetStream();
                tcp.ReceiveTimeout = 10000;
                OnlineWire.WriteFrame(s, new OnlineMessage { Op = "link-accept", V = OnlineWire.Version, Link = linkId, Player = SelfId, Key = _o.Key });
                var reply = OnlineWire.ReadFrame(s);
                if (!reply.Ok) { tcp.Dispose(); return; }
                tcp.ReceiveTimeout = 0;
                if (!hostSocket.Adopt(tcp)) tcp.Dispose();
                else Log($"relay: {reply.Player} joined session {sessionId} through the server");
            }
            catch (Exception e)
            {
                tcp?.Dispose();
                Log($"relay link for session {sessionId} failed: {e.GetBaseException().Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _wake.Set();
            Drop();
            _embedded?.Dispose();
            _embedded = null;
        }

        // ── The session handle ─────────────────────────────────────

        internal sealed class OnlineSession : IHostSession
        {
            readonly OnlineMultiplayerService _svc;
            readonly SessionView _view;
            readonly string _self;
            readonly bool _networked;
            volatile SessionRecord _last;
            volatile Dictionary<string, PropRecord> _saved;
            volatile string _goneMessage;
            SessionError _goneError;
            bool _closed;

            public OnlineSession(OnlineMultiplayerService svc, SessionRecord rec, string self, Dictionary<string, PropRecord> props)
            {
                _svc = svc;
                _self = self;
                _networked = rec.Relay;
                Id = rec.Id;
                Code = rec.Code;
                MaxPlayers = rec.MaxPlayers;
                _saved = props;
                _view = new SessionView(self, rec.Players.FirstOrDefault(p => p.Id == self)?.Properties ?? props);
                Apply(rec);
            }

            /// <summary>The last record this handle read: what a reconnect re-asserts.</summary>
            internal SessionRecord LastRecord => _last;
            internal Dictionary<string, PropRecord> SavedProps => _saved;

            /// <summary>A re-assert found the session gone: the next refresh reports it, as the server would.</summary>
            internal void MarkGone(SessionError error, string message)
            {
                _goneError = error;
                _goneMessage = message ?? $"Session '{Id}' is gone.";
            }

            public string Id { get; }
            public string Code { get; }
            public bool IsHost => _view.HostId == _self;
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
                if (rec == null) return;
                _last = rec;
                foreach (var id in _view.Apply(rec)) PlayerLeaving?.Invoke(id);
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
                if (_goneMessage != null) throw Gone(_goneError, _goneMessage);
                OnlineMessage reply;
                try { reply = await _svc.Request(new OnlineMessage { Op = "refresh", Sid = Id }); }
                catch (SessionException e) when (e.Error is SessionError.SessionDeleted or SessionError.NotInLobby)
                {
                    throw Gone(e.Error, e.Message);
                }
                if (_closed) return;
                Apply(reply.Session);
            }

            public async Task SaveCurrentPlayerDataAsync()
            {
                if (_closed) throw new SessionException(SessionError.NotInLobby, "Not in the session.");
                var props = _view.Self.Snapshot();
                await _svc.Request(new OnlineMessage { Op = "save", Sid = Id, Props = props });
                _saved = props;
            }

            /// <summary>Best effort, as the shared-folder backend: the handle closes even when the server cannot be told (it times the player out).</summary>
            public async Task LeaveAsync()
            {
                if (!_closed)
                    try { await _svc.Request(new OnlineMessage { Op = "leave", Sid = Id }); }
                    catch (SessionException) { }
                Close();
            }

            public async Task DeleteAsync()
            {
                if (!_closed)
                    try { await _svc.Request(new OnlineMessage { Op = "delete", Sid = Id }); }
                    catch (SessionException) { }
                Close();
                Deleted?.Invoke();
            }

            public async Task RemovePlayerAsync(string playerId)
            {
                await _svc.Request(new OnlineMessage { Op = "remove", Sid = Id, Player = playerId });
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

    /// <summary>
    /// The netcode transport while the online service is active: the host listens on TCP as
    /// before and also takes the clients the server relays to it; a client whose connection
    /// address names a session (<see cref="OnlineWire.RelayScheme"/>) connects through the relay.
    /// Any other address is a plain TCP connection.
    /// </summary>
    /// <summary>
    /// The online server's relay over the transport chosen beneath it (<see cref="Inner"/>: UDP or TCP, and the
    /// configured Unity-Relay-protocol relay). A host whose session allocated on that relay listens there; any other
    /// host listens on TCP and takes the links the server relays to it. A join to <c>amoebius-relay:SESSION</c>
    /// goes through the server; every other address (a relay join, a direct endpoint) goes to <see cref="Inner"/>.
    /// </summary>
    internal sealed class OnlineTransportFactory : INetTransportFactory, IWrappingTransportFactory
    {
        readonly OnlineMultiplayerService _svc;

        public OnlineTransportFactory(OnlineMultiplayerService svc, INetTransportFactory inner = null)
        {
            _svc = svc;
            Inner = inner is OnlineTransportFactory nested ? nested.Inner : inner ?? new TcpTransportFactory();
        }

        public INetTransportFactory Inner { get; }

        public INetTransport Listen(string address, int port)
        {
            if (RelaySessions.PendingHost != null) return Inner.Listen(address, port);
            var socket = NetSocket.Listen(address, port);
            _svc.AttachHost(socket);
            return socket;
        }

        public INetTransport Connect(string address, int port, int timeoutMs)
        {
            if (address != null && address.StartsWith(OnlineWire.RelayScheme, StringComparison.Ordinal))
            {
                var sessionId = address.Substring(OnlineWire.RelayScheme.Length);
                return NetSocket.ConnectWith(() => _svc.OpenRelayLink(sessionId, timeoutMs), "the relay");
            }
            return Inner.Connect(address, port, timeoutMs);
        }
    }
}
