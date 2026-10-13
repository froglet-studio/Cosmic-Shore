using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>How an <see cref="OnlineServer"/> runs.</summary>
    public sealed class OnlineServerOptions
    {
        /// <summary>TCP port (0: any free port). Discovery always answers on 47777/UDP.</summary>
        public int Port { get; set; } = OnlineWire.DefaultPort;
        /// <summary>A shared secret every client must present (empty: open to anyone who can reach the port).</summary>
        public string Key { get; set; }
        /// <summary>Hosted on purpose (<c>CosmicShore --server</c>, the launcher): never stands down for another server.</summary>
        public bool Standalone { get; set; }
        /// <summary>Answer LAN probes and announce, so games on the network find this server; merge with a better one.</summary>
        public bool Discovery { get; set; } = true;
        public string Name { get; set; }
        public Action<string> Log { get; set; }
    }

    /// <summary>
    /// Amoebius's online server: what the UGS Lobby and Relay are to the Unity build, for players on
    /// different machines. It holds the sessions (the presence lobby, parties, match sessions: one
    /// roster each, with the per-player properties the game invites through) under the same rules
    /// as the single-machine backend (<see cref="SessionRules"/>), and relays each joining client's
    /// netcode connection to the session's host, so nobody but the server needs a reachable address.
    ///
    /// Lobby state is soft: a game that reconnects (to this server or another) re-asserts every
    /// session it holds, so a server that restarts or stands down loses nothing a reconnect cannot
    /// rebuild. A player is alive while its control connection is up and for
    /// <see cref="SessionRules.PlayerTimeout"/> after it drops (time to reconnect).
    ///
    /// On a LAN it answers discovery probes and announces itself. A server hosted inside a game
    /// (the automatic mode) stands down for a better one it hears - a standalone one, else the
    /// older - telling its clients where to go; relay links already running keep running.
    /// Wire format: <see cref="OnlineWire"/>.
    /// </summary>
    public sealed class OnlineServer : IDisposable
    {
        const int LinkTimeoutMs = 8000;
        /// <summary>A control connection silent this long is gone (games ping every 5 s; tests shorten it).</summary>
        internal static int ControlSilenceMs { get; set; } = 30000;
        const int AnnounceMs = 3000;
        static readonly TimeSpan TombstoneLife = TimeSpan.FromMinutes(10);

        sealed class Control
        {
            public TcpClient Client;
            public NetworkStream Stream;
            public string Player;
            public string Address;
            public readonly object SendLock = new();
            public volatile bool Closed;

            public void Close()
            {
                Closed = true;
                try { Client.Close(); } catch { }
            }
        }

        sealed class PendingLink
        {
            public string Id;
            public string Sid;
            public string Host;
            public readonly TaskCompletionSource<TcpClient> HostSide = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        readonly OnlineServerOptions _o;
        readonly object _gate = new();
        readonly Dictionary<string, SessionRecord> _sessions = new();
        readonly Dictionary<string, Control> _controls = new();
        readonly Dictionary<string, long> _deleted = new();
        readonly Dictionary<string, long> _removed = new();
        readonly Dictionary<string, PendingLink> _links = new();
        readonly ConcurrentDictionary<TcpClient, byte> _pipeSockets = new();
        readonly Dictionary<string, long> _mergeChecked = new();

        TcpListener _listener;
        UdpClient _udp;
        Timer _announce, _sweep;
        volatile bool _disposed, _yielded;
        int _pipes;

        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        public long Started { get; } = DateTime.UtcNow.Ticks;
        public int Port { get; private set; }
        public bool Standalone => _o.Standalone;
        public bool DiscoveryActive => _udp != null;
        /// <summary>Set once this server stood down for a better one (embedded servers only).</summary>
        public bool Yielded => _yielded;
        /// <summary>Raised (on a background thread) when this server stands down: the server everyone moves to.</summary>
        public event Action<string, int> StoodDown;

        OnlineServer(OnlineServerOptions o) => _o = o;

        void Log(string message) => (_o.Log ?? (m => Console.WriteLine("[online] " + m)))(message);

        /// <summary>
        /// Starts a server: binds the discovery port first when discovery is on (one server per
        /// machine owns it), then TCP. Throws when the TCP port is taken; a taken discovery port is
        /// logged and the server runs without LAN discovery.
        /// </summary>
        public static OnlineServer Start(OnlineServerOptions options)
        {
            var s = new OnlineServer(options ?? new OnlineServerOptions());
            if (s._o.Discovery && !s.TryBindDiscovery())
                s.Log($"UDP {OnlineWire.DiscoveryPort} is taken (another Amoebius server on this machine?): this server is not discoverable on the LAN. Players connect to it by address.");
            try { s.BindTcp(s._o.Port); }
            catch { s.Dispose(); throw; }
            s.Begin();
            return s;
        }

        /// <summary>
        /// The automatic mode's server inside a game: only when this machine has no server yet (the
        /// discovery port is the election lock). Returns null when another server owns it - probe again
        /// and connect to that one. Takes 47777/TCP when free, else any port.
        /// </summary>
        public static OnlineServer TryStartEmbedded(string key, Action<string> log)
        {
            var s = new OnlineServer(new OnlineServerOptions { Key = key, Standalone = false, Discovery = true, Log = log, Name = Environment.MachineName });
            if (!s.TryBindDiscovery()) { s.Dispose(); return null; }
            try { s.BindTcp(OnlineWire.DefaultPort); }
            catch (SocketException)
            {
                try { s.BindTcp(0); }
                catch { s.Dispose(); throw; }
            }
            s.Begin();
            return s;
        }

        bool TryBindDiscovery()
        {
            var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                try { udp.ExclusiveAddressUse = true; } catch (Exception) { }
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, OnlineWire.DiscoveryPort));
                udp.EnableBroadcast = true;
                OnlineWire.IgnoreConnectionResets(udp);
                _udp = udp;
                return true;
            }
            catch (SocketException)
            {
                udp.Dispose();
                return false;
            }
        }

        void BindTcp(int port)
        {
            TcpListener l;
            if (Socket.OSSupportsIPv6)
            {
                l = new TcpListener(IPAddress.IPv6Any, port);
                try { l.Server.DualMode = true; } catch (Exception) { l = new TcpListener(IPAddress.Any, port); }
            }
            else l = new TcpListener(IPAddress.Any, port);
            try { l.ExclusiveAddressUse = true; } catch (Exception) { }
            l.Start();
            _listener = l;
            Port = ((IPEndPoint)l.LocalEndpoint).Port;
        }

        void Begin()
        {
            new Thread(AcceptLoop) { IsBackground = true, Name = "online-accept" }.Start();
            if (_udp != null)
            {
                new Thread(DiscoveryLoop) { IsBackground = true, Name = "online-discovery" }.Start();
                _announce = new Timer(_ => Announce(), null, 200, AnnounceMs);
            }
            _sweep = new Timer(_ => { lock (_gate) Sweep(SessionRules.NowTicks); }, null, 15000, 15000);
            Log($"server {Id} listening on port {Port}{(_udp != null ? ", discoverable on the LAN" : "")}{(string.IsNullOrEmpty(_o.Key) ? "" : ", key required")}" +
                $"{(_o.Standalone ? "" : " (hosted by this game)")}");
        }

        internal ServerAnnouncement Me => new()
        {
            V = OnlineWire.Version, Id = Id, Port = Port, Started = Started, Standalone = _o.Standalone,
            Name = _o.Name ?? Environment.MachineName, Keyed = !string.IsNullOrEmpty(_o.Key),
        };

        /// <summary>One line for logs and the launcher: sessions, connected players, relay links.</summary>
        public string Status
        {
            get
            {
                lock (_gate)
                    return $"{_controls.Count} player(s) connected, {_sessions.Count} session(s), {Volatile.Read(ref _pipes)} relay link(s)";
            }
        }

        // ── Connections ─────────────────────────────────────────────

        void AcceptLoop()
        {
            while (!_disposed && !_yielded)
            {
                TcpClient c;
                try { c = _listener.AcceptTcpClient(); }
                catch { if (_disposed || _yielded) return; continue; }
                OnlineWire.Tune(c);
                new Thread(() => HandleConnection(c)) { IsBackground = true, Name = "online-conn" }.Start();
            }
        }

        bool KeyOk(string key) => string.IsNullOrEmpty(_o.Key) || string.Equals(_o.Key, key, StringComparison.Ordinal);

        static string RemoteAddress(TcpClient c)
        {
            try
            {
                var ip = ((IPEndPoint)c.Client.RemoteEndPoint).Address;
                if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
                return ip.ToString();
            }
            catch (Exception) { return "?"; }
        }

        void HandleConnection(TcpClient c)
        {
            NetworkStream stream;
            OnlineMessage first;
            try
            {
                stream = c.GetStream();
                c.ReceiveTimeout = 10000;
                first = OnlineWire.ReadFrame(stream);
                c.ReceiveTimeout = 0;
            }
            catch (Exception) { c.Dispose(); return; }

            if (first.Op == "status")
            {
                TryWrite(stream, new OnlineMessage { Rid = first.Rid, Ok = true, Server = Id, Msg = Status, Port = Port });
                c.Dispose();
                return;
            }
            if (first.V != OnlineWire.Version)
            {
                TryWrite(stream, Refuse(first, SessionError.Unknown, $"This online server speaks protocol v{OnlineWire.Version} and the game v{first.V}: update Amoebius on both machines."));
                c.Dispose();
                return;
            }
            if (!KeyOk(first.Key))
            {
                TryWrite(stream, Refuse(first, SessionError.Unknown, "Wrong server key."));
                c.Dispose();
                return;
            }
            switch (first.Op)
            {
                case "probe":
                    // Reachable, same protocol, key accepted: what a server checks before standing down for this one.
                    TryWrite(stream, new OnlineMessage { Rid = first.Rid, Ok = true, Server = Id, Port = Port });
                    c.Dispose();
                    break;
                case "hello": RunControl(c, stream, first); break;
                case "link-open": RunLinkOpen(c, stream, first); break;
                case "link-accept": RunLinkAccept(c, stream, first); break;
                default: c.Dispose(); break;
            }
        }

        static void TryWrite(Stream s, OnlineMessage m)
        {
            try { OnlineWire.WriteFrame(s, m); } catch (Exception) { }
        }

        static bool Send(Control ctl, OnlineMessage m)
        {
            if (ctl.Closed) return false;
            try
            {
                lock (ctl.SendLock) OnlineWire.WriteFrame(ctl.Stream, m);
                return true;
            }
            catch (Exception)
            {
                ctl.Close();
                return false;
            }
        }

        void RunControl(TcpClient c, NetworkStream stream, OnlineMessage hello)
        {
            if (string.IsNullOrEmpty(hello.Player)) { c.Dispose(); return; }
            var ctl = new Control { Client = c, Stream = stream, Player = hello.Player, Address = RemoteAddress(c) };
            Control replaced;
            lock (_gate)
            {
                if (_yielded) { c.Dispose(); return; }
                _controls.TryGetValue(ctl.Player, out replaced);
                _controls[ctl.Player] = ctl;
            }
            // A player's newer connection (a reconnect) retires its older one.
            replaced?.Close();
            Send(ctl, new OnlineMessage { Rid = hello.Rid, Ok = true, Server = Id, Addr = ctl.Address, V = OnlineWire.Version, Port = Port });
            Log($"{ctl.Player} connected from {ctl.Address}{(string.IsNullOrEmpty(hello.Build) ? "" : $" (build {hello.Build})")}");
            try
            {
                c.ReceiveTimeout = ControlSilenceMs;
                while (!ctl.Closed && !_disposed)
                {
                    var m = OnlineWire.ReadFrame(stream);
                    OnlineMessage reply;
                    try { reply = HandleRequest(ctl, m); }
                    catch (Exception e) { reply = Refuse(m, SessionError.Unknown, "Server error: " + e.Message); }
                    if (reply != null) Send(ctl, reply);
                }
            }
            catch (Exception) { }
            finally
            {
                bool current;
                lock (_gate)
                {
                    current = _controls.TryGetValue(ctl.Player, out var cur) && ReferenceEquals(cur, ctl);
                    if (current)
                    {
                        _controls.Remove(ctl.Player);
                        // The grace period starts now: the player keeps its seats while it reconnects.
                        long now = SessionRules.NowTicks;
                        foreach (var rec in _sessions.Values)
                            foreach (var p in rec.Players)
                                if (p.Id == ctl.Player) p.LastSeen = now;
                    }
                }
                ctl.Close();
                if (current) Log($"{ctl.Player} disconnected");
            }
        }

        // ── Lobby ───────────────────────────────────────────────────

        static OnlineMessage Ok(OnlineMessage request, SessionRecord rec = null) => new() { Rid = request.Rid, Ok = true, Session = rec };

        static OnlineMessage Refuse(OnlineMessage request, SessionError error, string message)
            => new() { Rid = request.Rid, Ok = false, Err = error.ToString(), Msg = message };

        static string RemovedKey(string sid, string player) => sid + "\n" + player;

        /// <summary>A connected player is alive: stamp every connected member of <paramref name="rec"/> as seen now.</summary>
        void Touch(SessionRecord rec, long now)
        {
            foreach (var p in rec.Players)
                if (_controls.ContainsKey(p.Id)) p.LastSeen = now;
        }

        void Sweep(long now)
        {
            foreach (var rec in _sessions.Values.ToList())
            {
                Touch(rec, now);
                if (SessionRules.Expired(rec, now)) _sessions.Remove(rec.Id);
            }
            long cutoff = now - TombstoneLife.Ticks;
            foreach (var k in _deleted.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList()) _deleted.Remove(k);
            foreach (var k in _removed.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList()) _removed.Remove(k);
        }

        OnlineMessage HandleRequest(Control ctl, OnlineMessage m)
        {
            long now = SessionRules.NowTicks;
            lock (_gate)
            {
                switch (m.Op)
                {
                    case "ping":
                        return Ok(m);

                    case "create":
                    {
                        var rec = m.Session;
                        if (rec == null || string.IsNullOrEmpty(rec.Id)) return Refuse(m, SessionError.Unknown, "No session to create.");
                        if (_sessions.ContainsKey(rec.Id) || _deleted.ContainsKey(rec.Id)) return Refuse(m, SessionError.Unknown, $"Session '{rec.Id}' already exists.");
                        var me = rec.Players?.FirstOrDefault(p => p.Id == ctl.Player) ?? new PlayerRecord { Id = ctl.Player };
                        me.Properties ??= new Dictionary<string, PropRecord>();
                        me.LastSeen = now;
                        rec.Players = new List<PlayerRecord> { me };
                        rec.HostPlayerId = ctl.Player;
                        rec.Created = now;
                        rec.Properties ??= new Dictionary<string, PropRecord>();
                        rec.RelayAddress = null;
                        rec.RelayPort = 0;
                        if (string.IsNullOrEmpty(rec.Code)) rec.Code = SessionRules.NewCode();
                        _sessions[rec.Id] = rec;
                        return Ok(m, rec);
                    }

                    case "join":
                    {
                        _sessions.TryGetValue(m.Sid ?? "", out var rec);
                        if (rec != null) Touch(rec, now);
                        if (SessionRules.Join(rec, m.Sid, ctl.Player, m.Props, now) is { } refused) return Refuse(m, refused.Error, refused.Message);
                        _removed.Remove(RemovedKey(m.Sid, ctl.Player));
                        return Ok(m, rec);
                    }

                    case "query":
                    {
                        Sweep(now);
                        var filters = m.Filters?.Select(f => f.ToOption()).ToList();
                        var found = new List<SessionSummary>();
                        foreach (var rec in _sessions.Values)
                        {
                            if (!SessionRules.Listed(rec, filters, now)) continue;
                            SessionRules.Prune(rec, now);
                            found.Add(SessionSummary.From(rec));
                        }
                        return new OnlineMessage { Rid = m.Rid, Ok = true, Found = found };
                    }

                    case "refresh":
                    {
                        if (!_sessions.TryGetValue(m.Sid ?? "", out var rec)) return Refuse(m, SessionError.SessionDeleted, $"Session '{m.Sid}' is gone.");
                        Touch(rec, now);
                        if (!SessionRules.Refresh(rec, ctl.Player, now)) return Refuse(m, SessionError.NotInLobby, $"Not in session '{m.Sid}'.");
                        return Ok(m, rec);
                    }

                    case "save":
                    {
                        var me = _sessions.TryGetValue(m.Sid ?? "", out var rec) ? rec.Players.FirstOrDefault(p => p.Id == ctl.Player) : null;
                        if (me == null) return Refuse(m, SessionError.NotInLobby, $"Session '{m.Sid}' is gone.");
                        me.Properties = m.Props ?? new Dictionary<string, PropRecord>();
                        me.LastSeen = now;
                        return Ok(m);
                    }

                    case "leave":
                    {
                        if (_sessions.TryGetValue(m.Sid ?? "", out var rec) && SessionRules.Leave(rec, ctl.Player))
                        {
                            _sessions.Remove(rec.Id);
                            _deleted[rec.Id] = now;
                        }
                        return Ok(m);
                    }

                    case "delete":
                    {
                        if (_sessions.TryGetValue(m.Sid ?? "", out var rec))
                        {
                            if (rec.HostPlayerId != ctl.Player) return Refuse(m, SessionError.Unknown, "Only the host can delete a session.");
                            _sessions.Remove(rec.Id);
                            _deleted[rec.Id] = now;
                        }
                        return Ok(m);
                    }

                    case "remove":
                    {
                        if (_sessions.TryGetValue(m.Sid ?? "", out var rec))
                        {
                            if (rec.HostPlayerId != ctl.Player) return Refuse(m, SessionError.Unknown, "Only the host can remove a player.");
                            rec.Players.RemoveAll(p => p.Id == m.Player);
                            _removed[RemovedKey(rec.Id, m.Player)] = now;
                        }
                        return Ok(m);
                    }

                    case "assert":
                        return Assert(ctl, m, now);

                    case "link-reject":
                        if (_links.TryGetValue(m.Link ?? "", out var link)) link.HostSide.TrySetResult(null);
                        return null;

                    default:
                        return Refuse(m, SessionError.Unknown, $"Unknown request '{m.Op}'.");
                }
            }
        }

        /// <summary>
        /// A reconnected game re-asserts a session it holds: the server keeps what it has, or
        /// rebuilds the session from the game's last copy (every member it listed gets the reconnect
        /// grace, so the host keeps its seat until it reconnects too). The asserter's own entry is
        /// upserted; the host's assert also restores the session's settings. A deleted session, or a
        /// player the host removed, stays gone.
        /// </summary>
        OnlineMessage Assert(Control ctl, OnlineMessage m, long now)
        {
            var last = m.Session;
            if (last == null || string.IsNullOrEmpty(last.Id)) return Refuse(m, SessionError.Unknown, "No session to assert.");
            if (_deleted.ContainsKey(last.Id)) return Refuse(m, SessionError.SessionDeleted, $"Session '{last.Id}' was deleted.");
            if (_removed.ContainsKey(RemovedKey(last.Id, ctl.Player))) return Refuse(m, SessionError.NotInLobby, $"Removed from session '{last.Id}'.");
            if (!_sessions.TryGetValue(last.Id, out var rec))
            {
                rec = new SessionRecord
                {
                    Id = last.Id, Code = string.IsNullOrEmpty(last.Code) ? SessionRules.NewCode() : last.Code, Created = last.Created,
                    HostPlayerId = last.HostPlayerId, MaxPlayers = last.MaxPlayers, IsLocked = last.IsLocked, IsPrivate = last.IsPrivate,
                    Relay = last.Relay, Properties = last.Properties ?? new Dictionary<string, PropRecord>(),
                };
                if (last.Players != null)
                    foreach (var p in last.Players)
                        if (!string.IsNullOrEmpty(p.Id) && p.Id != ctl.Player)
                            rec.Players.Add(new PlayerRecord { Id = p.Id, Properties = p.Properties ?? new Dictionary<string, PropRecord>(), LastSeen = now });
                _sessions[rec.Id] = rec;
            }
            Touch(rec, now);
            var me = rec.Players.FirstOrDefault(p => p.Id == ctl.Player);
            if (me == null)
            {
                if (rec.IsLocked && rec.HostPlayerId != ctl.Player) return Refuse(m, SessionError.NotInLobby, "Session is locked.");
                if (rec.MaxPlayers > 0 && rec.Players.Count >= rec.MaxPlayers) return Refuse(m, SessionError.NotInLobby, "Session is full.");
                me = new PlayerRecord { Id = ctl.Player };
                rec.Players.Add(me);
            }
            if (m.Props != null) me.Properties = m.Props;
            me.LastSeen = now;
            if (rec.HostPlayerId == ctl.Player)
            {
                rec.Properties = last.Properties ?? rec.Properties;
                rec.MaxPlayers = last.MaxPlayers;
                rec.IsLocked = last.IsLocked;
                rec.IsPrivate = last.IsPrivate;
                rec.Relay = last.Relay;
            }
            return Ok(m, rec);
        }

        // ── Relay ───────────────────────────────────────────────────

        void RunLinkOpen(TcpClient c, NetworkStream stream, OnlineMessage open)
        {
            PendingLink link;
            Control host;
            lock (_gate)
            {
                if (!_sessions.TryGetValue(open.Sid ?? "", out var rec) || !rec.Relay)
                {
                    TryWrite(stream, Refuse(open, SessionError.SessionNotFound, $"No networked session '{open.Sid}'."));
                    c.Dispose();
                    return;
                }
                if (rec.Players.All(p => p.Id != open.Player))
                {
                    TryWrite(stream, Refuse(open, SessionError.NotInLobby, "Join the session before connecting to it."));
                    c.Dispose();
                    return;
                }
                if (!_controls.TryGetValue(rec.HostPlayerId, out host))
                {
                    TryWrite(stream, Refuse(open, SessionError.Unknown, "The session's host is not connected to the server."));
                    c.Dispose();
                    return;
                }
                link = new PendingLink { Id = Guid.NewGuid().ToString("N"), Sid = rec.Id, Host = rec.HostPlayerId };
                _links[link.Id] = link;
            }

            TcpClient hostSide = null;
            if (Send(host, new OnlineMessage { Ev = "link", Sid = link.Sid, Link = link.Id, Player = open.Player }))
            {
                try { if (link.HostSide.Task.Wait(LinkTimeoutMs)) hostSide = link.HostSide.Task.Result; }
                catch (Exception) { }
            }
            lock (_gate) _links.Remove(link.Id);
            if (hostSide == null)
            {
                link.HostSide.TrySetResult(null);
                TryWrite(stream, Refuse(open, SessionError.Unknown, "The host did not accept the connection."));
                c.Dispose();
                return;
            }

            try
            {
                OnlineWire.WriteFrame(hostSide.GetStream(), new OnlineMessage { Ok = true, Sid = link.Sid, Player = open.Player });
                OnlineWire.WriteFrame(stream, new OnlineMessage { Rid = open.Rid, Ok = true, Sid = link.Sid });
            }
            catch (Exception)
            {
                hostSide.Dispose();
                c.Dispose();
                return;
            }
            Log($"relay: {open.Player} <-> host {link.Host} (session {link.Sid})");
            Pipe(c, hostSide);
        }

        void RunLinkAccept(TcpClient c, NetworkStream stream, OnlineMessage accept)
        {
            PendingLink link;
            lock (_gate) _links.TryGetValue(accept.Link ?? "", out link);
            if (link == null || link.Host != accept.Player)
            {
                TryWrite(stream, Refuse(accept, SessionError.Unknown, "No such relay link."));
                c.Dispose();
                return;
            }
            // The opening side pairs the two and runs the pipe from here.
            if (!link.HostSide.TrySetResult(c)) c.Dispose();
        }

        /// <summary>Copies bytes both ways until either side closes, then closes both.</summary>
        void Pipe(TcpClient a, TcpClient b)
        {
            Interlocked.Increment(ref _pipes);
            _pipeSockets[a] = 0;
            _pipeSockets[b] = 0;
            try
            {
                var sa = a.GetStream();
                var sb = b.GetStream();
                var ab = Task.Factory.StartNew(() => Copy(sa, sb), TaskCreationOptions.LongRunning);
                var ba = Task.Factory.StartNew(() => Copy(sb, sa), TaskCreationOptions.LongRunning);
                Task.WaitAny(ab, ba);
            }
            catch (Exception) { }
            finally
            {
                try { a.Close(); } catch { }
                try { b.Close(); } catch { }
                _pipeSockets.TryRemove(a, out _);
                _pipeSockets.TryRemove(b, out _);
                Interlocked.Decrement(ref _pipes);
            }
        }

        static void Copy(Stream from, Stream to)
        {
            var buf = new byte[64 * 1024];
            try
            {
                int n;
                while ((n = from.Read(buf, 0, buf.Length)) > 0) to.Write(buf, 0, n);
            }
            catch (Exception) { }
        }

        // ── LAN discovery and merging ───────────────────────────────

        void DiscoveryLoop()
        {
            var udp = _udp;
            while (!_disposed && !_yielded && udp != null)
            {
                IPEndPoint from = new(IPAddress.Any, 0);
                byte[] data;
                try { data = udp.Receive(ref from); }
                catch (Exception) { if (_disposed || _yielded) return; continue; }
                string text;
                try { text = Encoding.UTF8.GetString(data); } catch (Exception) { continue; }
                if (text.StartsWith(OnlineWire.ProbePrefix, StringComparison.Ordinal))
                {
                    var answer = Encoding.UTF8.GetBytes(OnlineWire.EncodeAnnouncement(Me));
                    try { udp.Send(answer, answer.Length, from); } catch (Exception) { }
                }
                else if (OnlineWire.DecodeAnnouncement(text) is { } other && other.Id != Id && other.V == OnlineWire.Version)
                    ConsiderMerge(other, from.Address);
            }
        }

        void Announce()
        {
            var udp = _udp;
            if (udp == null || _disposed || _yielded) return;
            var bytes = Encoding.UTF8.GetBytes(OnlineWire.EncodeAnnouncement(Me));
            foreach (var target in OnlineDiscovery.BroadcastTargets())
                try { udp.Send(bytes, bytes.Length, new IPEndPoint(target, OnlineWire.DiscoveryPort)); } catch (Exception) { }
        }

        /// <summary>
        /// Heard another server: when it is the better one and this server lives inside a game,
        /// stand down for it (after checking it is really reachable, so a firewalled server cannot
        /// strand everyone).
        /// </summary>
        internal void ConsiderMerge(ServerAnnouncement other, IPAddress at)
        {
            if (_o.Standalone || _yielded || !ServerAnnouncement.Better(other, Me)) return;
            lock (_gate)
            {
                long now = DateTime.UtcNow.Ticks;
                if (_mergeChecked.TryGetValue(other.Id, out var t) && now - t < TimeSpan.FromSeconds(10).Ticks) return;
                _mergeChecked[other.Id] = now;
            }
            var addr = at.IsIPv4MappedToIPv6 ? at.MapToIPv4().ToString() : at.ToString();
            if (!Accepts(addr, other.Port, _o.Key)) return;
            StandDown(addr, other.Port, other);
        }

        /// <summary>True when the server at <paramref name="addr"/>:<paramref name="port"/> answers and takes <paramref name="key"/>.</summary>
        internal static bool Accepts(string addr, int port, string key)
        {
            try
            {
                using var c = OnlineWire.Connect(addr, port, 2000);
                var s = c.GetStream();
                c.ReceiveTimeout = 3000;
                OnlineWire.WriteFrame(s, new OnlineMessage { Op = "probe", V = OnlineWire.Version, Key = key });
                return OnlineWire.ReadFrame(s).Ok;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Stands down for the server at <paramref name="addr"/>:<paramref name="port"/>: no new
        /// connections, every game told to move there (each re-asserts its sessions on arrival);
        /// relay links already running keep running until they close.
        /// </summary>
        internal void StandDown(string addr, int port, ServerAnnouncement other = null)
        {
            List<Control> controls;
            lock (_gate)
            {
                if (_yielded) return;
                _yielded = true;
                controls = _controls.Values.ToList();
                _controls.Clear();
                foreach (var l in _links.Values) l.HostSide.TrySetResult(null);
            }
            Log($"standing down for server {other?.Id ?? "?"} at {addr}:{port}{(other?.Standalone == true ? " (standalone)" : "")}");
            try { _listener?.Stop(); } catch { }
            _announce?.Dispose();
            try { _udp?.Close(); } catch { }
            _udp = null;
            foreach (var ctl in controls)
            {
                Send(ctl, new OnlineMessage { Ev = "moved", Addr = addr, Port = port });
                ctl.Close();
            }
            StoodDown?.Invoke(addr, port);
        }

        /// <summary>Relay links still running (a stood-down server lives on until they close).</summary>
        public int RelayLinks => Volatile.Read(ref _pipes);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _listener?.Stop(); } catch { }
            _announce?.Dispose();
            _sweep?.Dispose();
            try { _udp?.Close(); } catch { }
            _udp = null;
            List<Control> controls;
            lock (_gate)
            {
                controls = _controls.Values.ToList();
                _controls.Clear();
                foreach (var l in _links.Values) l.HostSide.TrySetResult(null);
            }
            foreach (var ctl in controls) ctl.Close();
            foreach (var s in _pipeSockets.Keys) try { s.Close(); } catch { }
        }
    }
}
