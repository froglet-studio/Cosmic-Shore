using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// What this process's netcode sent and received: Unity's Runtime Network Stats Monitor and the
    /// counting half of its Network Profiler, for Prisma (docs/MULTIPLAYER.md §6.4).
    ///
    /// Counts are of whole frames as the driver hands them to the transport (the payload, without the
    /// transport's own framing), per peer, per message kind and per RPC method. Round-trip time comes
    /// from the driver's clock ping: a client measures it, and reports it to the server inside its
    /// next ping, so the host sees every member's RTT too. Everything runs on the main thread.
    ///
    /// Read it with <c>do net</c> (the player), MCP <c>net_stats</c>, or the session report's
    /// <c>net</c> block. <c>do net capture N</c> records N frames of per-frame counters to JSON.
    /// </summary>
    public static class NetStats
    {
        public sealed class PeerStats
        {
            public ulong ClientId;
            public long BytesIn, BytesOut, MsgsIn, MsgsOut;
            /// <summary>Smoothed round trip in ms; -1 until measured.</summary>
            public double RttMs = -1;
            /// <summary>The latest sample, and the lowest seen (the line's floor: a frame or two each way plus the network).</summary>
            public double LastRttMs = -1, MinRttMs = -1;
        }

        sealed class Counter { public long Calls, Bytes; }

        /// <summary>The server, as a client sees it.</summary>
        public const ulong ServerPeer = 0;
        /// <summary>Connections the server has not approved yet (no client id) count as PendingPeer - transportPeer.</summary>
        public const ulong PendingPeer = ulong.MaxValue;

        static readonly long[] s_kindMsgsIn = new long[256], s_kindBytesIn = new long[256];
        static readonly long[] s_kindMsgsOut = new long[256], s_kindBytesOut = new long[256];
        static readonly Dictionary<string, Counter> s_rpcIn = new(), s_rpcOut = new();
        static readonly Dictionary<ulong, PeerStats> s_peers = new();

        public static long BytesIn { get; private set; }
        public static long BytesOut { get; private set; }
        public static long MsgsIn { get; private set; }
        public static long MsgsOut { get; private set; }
        /// <summary>True once anything was counted since the last <see cref="Reset"/>.</summary>
        public static bool Any => MsgsIn + MsgsOut > 0;

        /// <summary>Bytes per second over the last whole second (updated once a second by <see cref="EndFrame"/>).</summary>
        public static double BytesInPerSecond { get; private set; }
        public static double BytesOutPerSecond { get; private set; }
        /// <summary>Highest one-second rate seen since the last reset.</summary>
        public static double PeakBytesInPerSecond { get; private set; }
        public static double PeakBytesOutPerSecond { get; private set; }

        /// <summary>Names a message kind byte (the driver installs its enum's names).</summary>
        internal static Func<byte, string> KindName = b => b.ToString();

        static double s_windowStart = -1;
        static long s_windowIn, s_windowOut;

        // ── Counting (called by NetDriver) ──────────────────────────

        /// <summary>Frames sent / received on the unreliable channel (they are in every other count too).</summary>
        public static long UnreliableOut { get; private set; }
        public static long UnreliableIn { get; private set; }

        internal static void Sent(ulong peer, byte[] payload, NetChannel channel = NetChannel.Reliable)
        {
            if (payload == null || payload.Length == 0) return;
            if (channel == NetChannel.Unreliable) UnreliableOut++;
            int n = payload.Length;
            BytesOut += n; MsgsOut++;
            s_kindMsgsOut[payload[0]]++; s_kindBytesOut[payload[0]] += n;
            var p = Peer(peer); p.BytesOut += n; p.MsgsOut++;
            if (payload[0] == NetDriver.RpcKindByte) CountRpc(s_rpcOut, payload);
        }

        internal static void Received(ulong peer, byte[] payload, NetChannel channel = NetChannel.Reliable)
        {
            if (payload == null || payload.Length == 0) return;
            if (channel == NetChannel.Unreliable) UnreliableIn++;
            int n = payload.Length;
            BytesIn += n; MsgsIn++;
            s_kindMsgsIn[payload[0]]++; s_kindBytesIn[payload[0]] += n;
            var p = Peer(peer); p.BytesIn += n; p.MsgsIn++;
            if (payload[0] == NetDriver.RpcKindByte) CountRpc(s_rpcIn, payload);
        }

        /// <summary>
        /// Folds a round-trip sample into the peer's RTT. Samples come once a second, so the weight is
        /// 1/4 (TCP's 1/8 suits per-packet samples): a change of line settles in ~10 s, and a scene
        /// load's stalled samples stop dominating soon after it ends.
        /// </summary>
        internal static void RttSample(ulong peer, double ms)
        {
            if (ms < 0 || double.IsNaN(ms)) return;
            var p = Peer(peer);
            p.RttMs = p.RttMs < 0 ? ms : p.RttMs + (ms - p.RttMs) * 0.25;
            p.LastRttMs = ms;
            p.MinRttMs = p.MinRttMs < 0 ? ms : Math.Min(p.MinRttMs, ms);
        }

        /// <summary>A client's own latest RTT sample, reported in its ping: folded in like a sample of our own.</summary>
        internal static void RttReport(ulong peer, double ms) => RttSample(peer, ms);

        /// <summary>A peer's id changed when the server approved it (its counters move with it).</summary>
        internal static void Rekey(ulong from, ulong to)
        {
            if (!s_peers.Remove(from, out var p)) return;
            if (s_peers.TryGetValue(to, out var existing))
            {
                existing.BytesIn += p.BytesIn; existing.BytesOut += p.BytesOut;
                existing.MsgsIn += p.MsgsIn; existing.MsgsOut += p.MsgsOut;
                return;
            }
            p.ClientId = to;
            s_peers[to] = p;
        }

        internal static void Forget(ulong peer) => s_peers.Remove(peer);

        static PeerStats Peer(ulong id)
        {
            if (!s_peers.TryGetValue(id, out var p)) s_peers[id] = p = new PeerStats { ClientId = id };
            return p;
        }

        /// <summary>An RPC payload is [kind][u64 object][u16 behaviour][string method]...: count it by method.</summary>
        static void CountRpc(Dictionary<string, Counter> table, byte[] payload)
        {
            string method = "?";
            if (payload.Length > 11)
            {
                try
                {
                    using var r = new BinaryReader(new MemoryStream(payload, 11, payload.Length - 11));
                    method = r.ReadString();
                }
                catch (Exception) { }
            }
            if (!table.TryGetValue(method, out var c)) table[method] = c = new Counter();
            c.Calls++;
            c.Bytes += payload.Length;
        }

        // ── Per frame: rates and capture ────────────────────────────

        static List<object> s_capture;
        static int s_captureFrames;
        static string s_capturePath;
        static long s_capIn, s_capOut, s_capMsgsIn, s_capMsgsOut;
        static int s_frame;

        /// <summary>Once per frame while the driver runs: rolls the one-second rate window and records a capture frame.</summary>
        internal static void EndFrame(double nowSeconds)
        {
            s_frame++;
            if (s_windowStart < 0) { s_windowStart = nowSeconds; s_windowIn = BytesIn; s_windowOut = BytesOut; }
            else if (nowSeconds - s_windowStart >= 1.0)
            {
                double span = nowSeconds - s_windowStart;
                BytesInPerSecond = (BytesIn - s_windowIn) / span;
                BytesOutPerSecond = (BytesOut - s_windowOut) / span;
                PeakBytesInPerSecond = Math.Max(PeakBytesInPerSecond, BytesInPerSecond);
                PeakBytesOutPerSecond = Math.Max(PeakBytesOutPerSecond, BytesOutPerSecond);
                s_windowStart = nowSeconds; s_windowIn = BytesIn; s_windowOut = BytesOut;
            }
            if (s_capture == null) return;
            s_capture.Add(new
            {
                frame = s_frame,
                t = Math.Round(nowSeconds, 4),
                bytesIn = BytesIn - s_capIn,
                bytesOut = BytesOut - s_capOut,
                msgsIn = MsgsIn - s_capMsgsIn,
                msgsOut = MsgsOut - s_capMsgsOut,
                rttMs = s_peers.Values.Where(p => p.RttMs >= 0).Select(p => Math.Round(p.RttMs, 1)).DefaultIfEmpty(-1).Max(),
            });
            s_capIn = BytesIn; s_capOut = BytesOut; s_capMsgsIn = MsgsIn; s_capMsgsOut = MsgsOut;
            if (s_capture.Count >= s_captureFrames) FinishCapture();
        }

        /// <summary>Starts recording <paramref name="frames"/> frames of per-frame counters; written to <paramref name="path"/> (a temp file by default).</summary>
        public static string BeginCapture(int frames, string path = null)
        {
            if (frames <= 0) return "[net] capture: frames must be > 0";
            s_capturePath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetTempPath(), $"prisma-net-{Environment.ProcessId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json")
                : Path.GetFullPath(path);
            s_capture = new List<object>(frames);
            s_captureFrames = frames;
            s_capIn = BytesIn; s_capOut = BytesOut; s_capMsgsIn = MsgsIn; s_capMsgsOut = MsgsOut;
            return $"[net] capturing {frames} frames -> {s_capturePath}";
        }

        static void FinishCapture()
        {
            var frames = s_capture;
            s_capture = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(s_capturePath)!);
                File.WriteAllText(s_capturePath, JsonSerializer.Serialize(new { kind = "prisma-net-capture", version = 1, frames, summary = Summary() },
                    new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"[net] capture written: {frames.Count} frames -> {s_capturePath}");
            }
            catch (Exception e) { Console.WriteLine($"[net] capture could not be written: {e.Message}"); }
        }

        // ── Reading ─────────────────────────────────────────────────

        public static void Reset()
        {
            Array.Clear(s_kindMsgsIn); Array.Clear(s_kindBytesIn); Array.Clear(s_kindMsgsOut); Array.Clear(s_kindBytesOut);
            s_rpcIn.Clear(); s_rpcOut.Clear(); s_peers.Clear();
            BytesIn = BytesOut = MsgsIn = MsgsOut = 0;
            UnreliableIn = UnreliableOut = 0;
            BytesInPerSecond = BytesOutPerSecond = PeakBytesInPerSecond = PeakBytesOutPerSecond = 0;
            s_windowStart = -1;
        }

        public static IReadOnlyCollection<PeerStats> Peers => s_peers.Values;

        static string PeerName(ulong id) => id > PendingPeer - 1_000_000 ? "pending" : id == ServerPeer && !NetDriver.IsServer ? "server" : "client " + id;

        /// <summary>A structured snapshot (the session report's <c>net</c> block and the capture's summary).</summary>
        public static object Summary()
        {
            object Kinds(long[] msgs, long[] bytes) => Enumerable.Range(0, 256).Where(i => msgs[i] > 0)
                .OrderByDescending(i => bytes[i])
                .Select(i => new { kind = KindName((byte)i), msgs = msgs[i], bytes = bytes[i] }).ToList();
            object Rpcs(Dictionary<string, Counter> t) => t.OrderByDescending(kv => kv.Value.Calls).Take(40)
                .Select(kv => new { method = kv.Key, calls = kv.Value.Calls, bytes = kv.Value.Bytes }).ToList();
            return new
            {
                role = NetDriver.IsServer ? "server" : NetDriver.IsClientOnly ? "client" : "off",
                bytesIn = BytesIn, bytesOut = BytesOut, msgsIn = MsgsIn, msgsOut = MsgsOut,
                unreliableIn = UnreliableIn, unreliableOut = UnreliableOut,
                bytesInPerSecond = Math.Round(BytesInPerSecond), bytesOutPerSecond = Math.Round(BytesOutPerSecond),
                peakBytesInPerSecond = Math.Round(PeakBytesInPerSecond), peakBytesOutPerSecond = Math.Round(PeakBytesOutPerSecond),
                simulator = NetSimulator.Settings.ToString(),
                peers = s_peers.Values.OrderBy(p => p.ClientId).Select(p => new
                {
                    peer = PeerName(p.ClientId),
                    rttMs = p.RttMs < 0 ? (double?)null : Math.Round(p.RttMs, 1),
                    lastRttMs = p.LastRttMs < 0 ? (double?)null : Math.Round(p.LastRttMs, 1),
                    minRttMs = p.MinRttMs < 0 ? (double?)null : Math.Round(p.MinRttMs, 1),
                    p.BytesIn, p.BytesOut, p.MsgsIn, p.MsgsOut,
                }).ToList(),
                kindsIn = Kinds(s_kindMsgsIn, s_kindBytesIn),
                kindsOut = Kinds(s_kindMsgsOut, s_kindBytesOut),
                rpcsIn = Rpcs(s_rpcIn),
                rpcsOut = Rpcs(s_rpcOut),
            };
        }

        static string Size(double b) => b >= 1 << 20 ? $"{b / (1 << 20):0.0} MB" : b >= 1024 ? $"{b / 1024:0.0} KB" : $"{b:0} B";

        /// <summary>The text <c>do net</c> prints and the overlay shows.</summary>
        public static string Report(int topRpcs = 8)
        {
            var sb = new StringBuilder();
            string role = NetDriver.IsServer ? $"server on port {NetDriver.ListenPort}" : NetDriver.IsClientOnly ? "client" : "not connected";
            sb.AppendLine($"[net] {role} · sim {NetSimulator.Settings}");
            sb.AppendLine($"[net] in  {Size(BytesInPerSecond)}/s (peak {Size(PeakBytesInPerSecond)}/s) · {MsgsIn} msgs · {Size(BytesIn)} total");
            sb.AppendLine($"[net] out {Size(BytesOutPerSecond)}/s (peak {Size(PeakBytesOutPerSecond)}/s) · {MsgsOut} msgs · {Size(BytesOut)} total");
            foreach (var p in s_peers.Values.OrderBy(p => p.ClientId))
                sb.AppendLine($"[net]   {PeerName(p.ClientId),-10} rtt {(p.RttMs < 0 ? "  -  " : $"{p.RttMs,5:0.0}")} ms (last {(p.LastRttMs < 0 ? "-" : $"{p.LastRttMs:0}")}, min {(p.MinRttMs < 0 ? "-" : $"{p.MinRttMs:0}")}) · in {Size(p.BytesIn)} ({p.MsgsIn}) · out {Size(p.BytesOut)} ({p.MsgsOut})");
            var kinds = Enumerable.Range(0, 256).Where(i => s_kindMsgsIn[i] + s_kindMsgsOut[i] > 0)
                .OrderByDescending(i => s_kindBytesIn[i] + s_kindBytesOut[i])
                .Select(i => $"{KindName((byte)i)} {s_kindMsgsOut[i]}/{s_kindMsgsIn[i]}");
            sb.AppendLine("[net] kinds out/in: " + string.Join(", ", kinds) + (UnreliableIn + UnreliableOut > 0 ? $" · unreliable {UnreliableOut}/{UnreliableIn}" : ""));
            var rpcs = s_rpcOut.Select(kv => (kv.Key, kv.Value.Calls, dir: "out")).Concat(s_rpcIn.Select(kv => (kv.Key, kv.Value.Calls, dir: "in")))
                .OrderByDescending(x => x.Calls).Take(topRpcs).Select(x => $"{x.Key} {x.dir} {x.Calls}");
            sb.Append("[net] top rpcs: " + string.Join(", ", rpcs));
            return sb.ToString();
        }

        /// <summary>A one-line monitor for the window title: who this player is and how its link is doing.</summary>
        public static string Title(string profile)
        {
            var sb = new StringBuilder("Cosmic Shore");
            if (!string.IsNullOrWhiteSpace(profile)) sb.Append(" · ").Append(profile.Trim());
            if (!NetDriver.IsActive) return sb.ToString();
            sb.Append(NetDriver.IsServer ? " · HOST" : " · CLIENT");
            sb.Append(NetRelay.TitleTag);
            double rtt = -1;
            foreach (var p in s_peers.Values) if (p.RttMs > rtt) rtt = p.RttMs;
            if (rtt >= 0) sb.Append($" · rtt {rtt:0} ms");
            sb.Append($" · in {Size(BytesInPerSecond)}/s out {Size(BytesOutPerSecond)}/s");
            if (NetSimulator.Settings.IsActive) sb.Append(" · sim ").Append(NetSimulator.Settings);
            return sb.ToString();
        }

        /// <summary>Handles the player's <c>do net ...</c> verb. Returns the text to print.</summary>
        public static string Command(string arg)
        {
            var parts = (arg ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return Report();
            switch (parts[0].ToLowerInvariant())
            {
                case "reset": Reset(); return "[net] counters reset";
                case "json": return JsonSerializer.Serialize(Summary());
                case "capture":
                    int frames = parts.Length > 1 && int.TryParse(parts[1], out var f) ? f : 600;
                    return BeginCapture(frames, parts.Length > 2 ? parts[2] : null);
                default: return "[net] usage: net | net reset | net json | net capture [FRAMES] [PATH]";
            }
        }
    }
}
