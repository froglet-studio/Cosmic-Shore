using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// What the network simulator does to this process's traffic (docs/MULTIPLAYER.md §6.2). Every
    /// delay applies to frames this process sends AND receives, so one process "behind a bad line"
    /// sees its round trip to every peer grow by twice <see cref="LatencyMs"/>.
    /// </summary>
    public sealed class NetSimSettings
    {
        /// <summary>One-way delay added to every frame, in both directions.</summary>
        public int LatencyMs;
        /// <summary>Random extra delay, 0..JitterMs. Frames stay in order: a late one holds back those behind it.</summary>
        public int JitterMs;
        /// <summary>Chance (0-100) that a frame is lost and resent. The stream is reliable, so a loss is a delay spike.</summary>
        public float LossPercent;
        /// <summary>Upload cap per peer in kilobits per second (0 = none).</summary>
        public int BandwidthKbps;
        /// <summary>The cable is pulled: nothing leaves or arrives. After <see cref="NetSimulator.DownDisconnectMs"/> every peer is dropped.</summary>
        public bool Down;

        public bool IsActive => LatencyMs > 0 || JitterMs > 0 || LossPercent > 0 || BandwidthKbps > 0 || Down;

        public NetSimSettings Clone() => (NetSimSettings)MemberwiseClone();

        public override string ToString()
        {
            if (!IsActive) return "off";
            var sb = new StringBuilder();
            sb.Append("latency=").Append(LatencyMs).Append(" jitter=").Append(JitterMs)
              .Append(" loss=").Append(LossPercent.ToString(CultureInfo.InvariantCulture))
              .Append(" bandwidth=").Append(BandwidthKbps);
            if (Down) sb.Append(" DOWN");
            return sb.ToString();
        }
    }

    /// <summary>
    /// The network simulator: Unity's Network Simulator for Prisma. Settings come from
    /// <c>COSMIC_SHORE_NET_SIM</c> at launch and from <c>do netsim SPEC</c> (or the MCP tool <c>net_sim</c>) while
    /// the game runs; every transport the driver opens reads them live through <see cref="SimulatedTransport"/>.
    ///
    /// SPEC is a list of tokens applied in order, separated by spaces or commas:
    ///   off | PRESET | down | up | latency=MS | jitter=MS | loss=PCT | bandwidth=KBPS
    /// e.g. "4g", "latency=80 jitter=20", "poor loss=10", "down".
    /// </summary>
    public static class NetSimulator
    {
        /// <summary>A pulled cable longer than this drops every peer, as Unity Transport's disconnect timeout does.</summary>
        public const int DownDisconnectMs = 10000;

        /// <summary>Presets after Unity's Network Simulator ones: latency, jitter, loss %, upload kbps.</summary>
        public static readonly IReadOnlyDictionary<string, (int latency, int jitter, float loss, int bandwidth)> Presets =
            new Dictionary<string, (int, int, float, int)>(StringComparer.OrdinalIgnoreCase)
            {
                ["lan"] = (1, 0, 0f, 0),
                ["broadband"] = (20, 5, 0f, 0),
                ["dsl"] = (40, 10, 0.5f, 0),
                ["4g"] = (60, 20, 1f, 0),
                ["3g"] = (120, 40, 2f, 0),
                ["poor"] = (200, 80, 5f, 128),
            };

        static NetSimSettings s_settings = FromEnvironment();

        /// <summary>The settings every simulated transport reads, each frame.</summary>
        public static NetSimSettings Settings
        {
            get => s_settings;
            set => s_settings = value ?? new NetSimSettings();
        }

        static NetSimSettings FromEnvironment()
        {
            var spec = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_SIM");
            if (string.IsNullOrWhiteSpace(spec)) return new NetSimSettings();
            if (TryParse(spec, new NetSimSettings(), out var s, out var error)) return s;
            Console.WriteLine($"[netsim] COSMIC_SHORE_NET_SIM ignored: {error}");
            return new NetSimSettings();
        }

        /// <summary>Applies SPEC to the current settings. Returns the line to print: the new settings or the error.</summary>
        public static string Apply(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return "[netsim] " + s_settings;
            if (!TryParse(spec, s_settings, out var next, out var error)) return "[netsim] error: " + error;
            s_settings = next;
            return "[netsim] " + next;
        }

        public static bool TryParse(string spec, NetSimSettings baseline, out NetSimSettings result, out string error)
        {
            result = (baseline ?? new NetSimSettings()).Clone();
            error = null;
            foreach (var raw in spec.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                int eq = token.IndexOf('=');
                if (eq < 0)
                {
                    if (token is "off" or "none" or "clear") { result = new NetSimSettings(); continue; }
                    if (token == "down") { result.Down = true; continue; }
                    if (token == "up") { result.Down = false; continue; }
                    if (Presets.TryGetValue(token, out var p))
                    {
                        (result.LatencyMs, result.JitterMs, result.LossPercent, result.BandwidthKbps) = p;
                        continue;
                    }
                    error = $"unknown token '{raw}' (off, down, up, {string.Join(", ", Presets.Keys)}, latency=, jitter=, loss=, bandwidth=)";
                    return false;
                }
                string key = token[..eq], value = token[(eq + 1)..];
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || v < 0)
                {
                    error = $"'{raw}': the value must be a number >= 0";
                    return false;
                }
                switch (key)
                {
                    case "latency": result.LatencyMs = (int)v; break;
                    case "jitter": result.JitterMs = (int)v; break;
                    case "loss": result.LossPercent = Math.Min(100f, v); break;
                    case "bandwidth": result.BandwidthKbps = (int)v; break;
                    default:
                        error = $"unknown setting '{key}' (latency, jitter, loss, bandwidth)";
                        return false;
                }
            }
            return true;
        }

        /// <summary>Wraps the driver's transport factory once, so every transport it opens is simulated.</summary>
        public static void Install()
        {
            if (NetDriver.TransportFactory is not SimulatedTransportFactory)
                NetDriver.TransportFactory = new SimulatedTransportFactory(NetDriver.TransportFactory, () => s_settings);
        }
    }

    internal sealed class SimulatedTransportFactory : INetTransportFactory
    {
        readonly INetTransportFactory _inner;
        readonly Func<NetSimSettings> _settings;

        public SimulatedTransportFactory(INetTransportFactory inner, Func<NetSimSettings> settings)
        {
            _inner = inner;
            _settings = settings;
        }

        public INetTransport Listen(string address, int port) => new SimulatedTransport(_inner.Listen(address, port), _settings);
        public INetTransport Connect(string address, int port, int timeoutMs) => new SimulatedTransport(_inner.Connect(address, port, timeoutMs), _settings);
    }

    /// <summary>
    /// Wraps any <see cref="INetTransport"/> and delays what it carries per <see cref="NetSimSettings"/>,
    /// keeping the transport contract (reliable, ordered, whole frames). Arriving frames are held and
    /// released by <see cref="Poll"/> (the driver polls every frame, so they have one-frame resolution,
    /// as anything the game receives does). Leaving frames are released on their due time by a small
    /// pump thread, so a sender that is busy loading still delivers on time. With the settings off and
    /// nothing held, every call passes straight through.
    /// </summary>
    internal sealed class SimulatedTransport : INetTransport
    {
        readonly struct Held
        {
            public readonly double Due;
            public readonly byte[] Data;   // null = a deferred Disconnect
            public readonly NetChannel Channel;
            public Held(double due, byte[] data, NetChannel channel = NetChannel.Reliable) { Due = due; Data = data; Channel = channel; }
        }

        sealed class PeerOut
        {
            public readonly Queue<Held> Queue = new();
            /// <summary>Unreliable frames: their own line, so a reliable resend spike never holds them back.</summary>
            public readonly Queue<Held> Unreliable = new();
            public double LastDue, NextFree, LastUnreliableDue;
            public int Count => Queue.Count + Unreliable.Count;
        }

        readonly INetTransport _inner;
        readonly Func<NetSimSettings> _settings;
        readonly Func<double> _nowMs;
        readonly System.Random _rng;
        readonly Dictionary<int, PeerOut> _out = new();   // guarded by _lock (the pump thread releases it)
        readonly object _lock = new();
        readonly System.Threading.Thread _pump;
        volatile bool _disposed;
        readonly Queue<(double due, NetEvent e)> _in = new();
        readonly Queue<(double due, NetEvent e)> _inUnreliable = new();
        double _lastInUnreliableDue;
        /// <summary>Unreliable frames the simulated line lost, both directions.</summary>
        public long UnreliableDropped;
        readonly HashSet<int> _peers = new();
        double _lastInDue;
        double _downSince = -1;
        bool _cut;

        public SimulatedTransport(INetTransport inner, Func<NetSimSettings> settings, Func<double> nowMs = null, int seed = 0)
        {
            _inner = inner;
            _settings = settings;
            bool realClock = nowMs == null;
            if (realClock)
            {
                long origin = System.Diagnostics.Stopwatch.GetTimestamp();
                nowMs = () => System.Diagnostics.Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
            }
            _nowMs = nowMs;
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
            // On a fake clock (tests) the caller drives time and releases held frames by polling or sending.
            if (realClock)
            {
                _pump = new System.Threading.Thread(PumpLoop) { IsBackground = true, Name = "netsim-pump" };
                _pump.Start();
            }
        }

        void PumpLoop()
        {
            lock (_lock)
            {
                while (!_disposed)
                {
                    double now = _nowMs();
                    PumpOut(now, _settings());
                    double next = double.MaxValue;
                    foreach (var po in _out.Values)
                    {
                        if (po.Queue.Count > 0) next = Math.Min(next, po.Queue.Peek().Due);
                        if (po.Unreliable.Count > 0) next = Math.Min(next, po.Unreliable.Peek().Due);
                    }
                    // Wake at the next due frame; a Send pulses when it queues an earlier one. Cap the wait
                    // so a settings change (the cable coming back up) is seen within 50 ms.
                    int wait = next == double.MaxValue ? 50 : (int)Math.Clamp(Math.Ceiling(next - now), 1, 50);
                    System.Threading.Monitor.Wait(_lock, wait);
                }
            }
        }

        public bool IsServer => _inner.IsServer;
        public int ListenPort => _inner.ListenPort;
        public int PeerCount => _inner.PeerCount;

        bool Holding => _in.Count > 0 || _inUnreliable.Count > 0 || _out.Count > 0;   // read under _lock for _out

        double Delay(NetSimSettings s)
        {
            double d = s.LatencyMs;
            if (s.JitterMs > 0) d += _rng.NextDouble() * s.JitterMs;
            // A lost frame is resent after a round trip (the reliable stream's retransmit), so it arrives late.
            if (s.LossPercent > 0 && _rng.NextDouble() * 100.0 < s.LossPercent) d += 2.0 * s.LatencyMs + s.JitterMs + 20.0;
            return d;
        }

        public void Send(int peer, byte[] payload, int length = -1) => Send(peer, payload, length, NetChannel.Reliable);

        public void Send(int peer, byte[] payload, int length, NetChannel channel)
        {
            if (_cut) return;
            if (length < 0) length = payload.Length;
            var s = _settings();
            lock (_lock)
            {
                if (!s.IsActive && !Holding) { _inner.Send(peer, payload, length, channel); return; }
                Enqueue(peer, payload, length, s, channel);
                System.Threading.Monitor.Pulse(_lock);
            }
        }

        /// <summary>A lost unreliable frame is gone; it is never resent.</summary>
        bool Lost(NetSimSettings s) => s.LossPercent > 0 && _rng.NextDouble() * 100.0 < s.LossPercent;

        double UnreliableDelay(NetSimSettings s) => s.LatencyMs + (s.JitterMs > 0 ? _rng.NextDouble() * s.JitterMs : 0);

        void Enqueue(int peer, byte[] payload, int length, NetSimSettings s, NetChannel channel = NetChannel.Reliable)
        {
            double now = _nowMs();
            if (channel == NetChannel.Unreliable && Lost(s)) { UnreliableDropped++; return; }
            var copy = new byte[length];
            Buffer.BlockCopy(payload, 0, copy, 0, length);
            if (!_out.TryGetValue(peer, out var po)) _out[peer] = po = new PeerOut();
            if (channel == NetChannel.Unreliable)
            {
                double udue = now + UnreliableDelay(s);
                if (s.BandwidthKbps > 0)
                {
                    po.NextFree = Math.Max(now, po.NextFree) + length * 8.0 / s.BandwidthKbps;
                    udue = Math.Max(udue, po.NextFree);
                }
                // Its own queue keeps release order simple; jitter could reorder datagrams, which is allowed.
                udue = Math.Max(udue, po.LastUnreliableDue);
                po.LastUnreliableDue = udue;
                po.Unreliable.Enqueue(new Held(udue, copy, NetChannel.Unreliable));
                PumpOut(now, s);
                return;
            }
            double due = now + Delay(s);
            if (s.BandwidthKbps > 0)
            {
                // kbps = bits per millisecond: a frame occupies the line for len*8/kbps ms.
                po.NextFree = Math.Max(now, po.NextFree) + length * 8.0 / s.BandwidthKbps;
                due = Math.Max(due, po.NextFree);
            }
            due = Math.Max(due, po.LastDue);
            po.LastDue = due;
            po.Queue.Enqueue(new Held(due, copy));
            PumpOut(now, s);
        }

        public void Disconnect(int peer)
        {
            lock (_lock)
            {
                // Behind the frames already queued to that peer (a kick's reason must arrive before the close).
                if (_out.TryGetValue(peer, out var po) && po.Queue.Count > 0)
                {
                    po.Queue.Enqueue(new Held(po.LastDue, null));
                    return;
                }
                if (!IsServer && _out.Count > 0)
                {
                    foreach (var q in _out.Values) q.Queue.Enqueue(new Held(q.LastDue, null));
                    return;
                }
            }
            _inner.Disconnect(peer);
        }

        public bool Poll(out NetEvent e)
        {
            var s = _settings();
            double now = _nowMs();
            lock (_lock)
            {
                CheckCable(now, s);
                PumpOut(now, s);
            }
            if (!s.IsActive && _in.Count == 0 && _inUnreliable.Count == 0) return Track(_inner.Poll(out e), e);
            // Read everything the inner transport has into the held queues, delayed and in order.
            while (_inner.Poll(out var ie))
            {
                if (_cut && ie.Kind == NetEventKind.Data) continue;
                if (ie.Kind == NetEventKind.Data && ie.Channel == NetChannel.Unreliable)
                {
                    if (Lost(s)) { UnreliableDropped++; continue; }
                    double udue = Math.Max(now + UnreliableDelay(s), _lastInUnreliableDue);
                    _lastInUnreliableDue = udue;
                    _inUnreliable.Enqueue((udue, ie));
                    continue;
                }
                double due = _cut ? now : Math.Max(now + (ie.Kind == NetEventKind.Data ? Delay(s) : s.LatencyMs), _lastInDue);
                _lastInDue = due;
                _in.Enqueue((due, ie));
            }
            if (!s.Down || _cut)
            {
                // The earlier of the two lines' heads, if it is due.
                bool r = _in.Count > 0 && _in.Peek().due <= now;
                bool u = _inUnreliable.Count > 0 && _inUnreliable.Peek().due <= now && !_cut;
                if (r && (!u || _in.Peek().due <= _inUnreliable.Peek().due)) { e = _in.Dequeue().e; return Track(true, e); }
                if (u) { e = _inUnreliable.Dequeue().e; return true; }
            }
            e = default;
            return false;
        }

        bool Track(bool got, NetEvent e)
        {
            if (!got) return false;
            if (e.Kind == NetEventKind.Connected) _peers.Add(e.Peer);
            else if (e.Kind == NetEventKind.Disconnected) _peers.Remove(e.Peer);
            return true;
        }

        void CheckCable(double now, NetSimSettings s)
        {
            // Back up: the dropped peers stay dropped, but a listening server takes new connections again.
            if (!s.Down) { _downSince = -1; _cut = false; return; }
            if (_downSince < 0) { _downSince = now; return; }
            if (_cut || now - _downSince < NetSimulator.DownDisconnectMs) return;
            // The line was down past the timeout: every peer is gone, on both ends.
            _cut = true;
            Console.WriteLine($"[netsim] down for {NetSimulator.DownDisconnectMs} ms: dropping every peer");
            _out.Clear();
            if (IsServer) foreach (int p in new List<int>(_peers)) _inner.Disconnect(p);
            else _inner.Disconnect(0);
        }

        void PumpOut(double now, NetSimSettings s)
        {
            if (_out.Count == 0 || s.Down) return;
            List<int> empty = null;
            foreach (var kv in _out)
            {
                var q = kv.Value.Queue;
                while (q.Count > 0 && q.Peek().Due <= now)
                {
                    var h = q.Dequeue();
                    if (h.Data == null) _inner.Disconnect(kv.Key);
                    else _inner.Send(kv.Key, h.Data);
                }
                var uq = kv.Value.Unreliable;
                while (uq.Count > 0 && uq.Peek().Due <= now) _inner.Send(kv.Key, uq.Dequeue().Data, -1, NetChannel.Unreliable);
                if (kv.Value.Count == 0) (empty ??= new List<int>()).Add(kv.Key);
            }
            if (empty != null) foreach (int p in empty) _out.Remove(p);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
                _out.Clear();
                System.Threading.Monitor.Pulse(_lock);
            }
            _in.Clear();
            _inner.Dispose();
        }
    }
}
