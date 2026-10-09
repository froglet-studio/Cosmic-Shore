using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Prisma.RelaySpike
{
    /// <summary>
    /// relay-spike selftest [--dtls] [--region R] [--count N] [--rate HZ]
    ///     Two clients in one process: host allocates + makes a join code, joiner joins by code,
    ///     both BIND, joiner CONNECTs, then echo traffic both ways with RTT stats.
    /// relay-spike host [--dtls] [--region R] [--seconds S]     prints a join code, echoes whatever arrives
    /// relay-spike join CODE [--dtls] [--count N] [--rate HZ]   connects by code, measures RTT to the host
    /// relay-spike regions
    /// Common: --project ID (default: the game's cloudProjectId) --env NAME (default: development)
    /// Tokens are never printed. Allocations are not deleted (Relay has no delete); they expire on their own.
    /// </summary>
    public static class Program
    {
        // From ProjectSettings/ProjectSettings.asset (cloudProjectId) and
        // ProjectSettings/Packages/com.unity.services.core/Settings.json (EnvironmentName).
        const string DefaultProject = "3030fd69-28ab-433f-b4bd-22b9b93c5118";
        const string DefaultEnv = "development";

        const byte EchoRequest = 1, EchoReply = 2, ProbeRequest = 3, ProbeReply = 4;
        const int EchoHeader = 1 + 4 + 8; // kind, seq, sender timestamp

        public static async Task<int> Main(string[] args)
        {
            var a = new Args(args);
            string mode = a.Positional(0) ?? "selftest";
            using var ugs = new UgsClient(a.Get("--project", DefaultProject), a.Get("--env", DefaultEnv));
            Console.WriteLine($"project {ugs.ProjectId}  environment {ugs.Environment}  mode {mode}{(a.Has("--dtls") ? "  (dtls)" : "  (udp)")}");
            try
            {
                switch (mode)
                {
                    case "regions":
                        var p = await ugs.SignInAnonymouslyAsync();
                        Console.WriteLine(string.Join(", ", await ugs.ListRegionsAsync(p)));
                        return 0;
                    case "selftest": return await SelfTest(ugs, a);
                    case "dtls-probe": return await DtlsProbe(ugs);
                    case "host": return await Host(ugs, a);
                    case "join": return await Join(ugs, a, a.Positional(1) ?? throw new ArgumentException("join needs a CODE"));
                    default: Console.Error.WriteLine("modes: selftest | host | join CODE | regions"); return 2;
                }
            }
            catch (Exception e) when (e is UgsException or TimeoutException or ArgumentException)
            {
                Console.Error.WriteLine("FAILED: " + e.Message);
                return 1;
            }
        }

        static IDatagramChannel Open(RelayAllocation alloc, bool dtls)
        {
            var ep = alloc.Endpoint(dtls ? "dtls" : "udp");
            if (!dtls) return new UdpDatagramChannel(ep.Host, ep.Port);
            var sw = Stopwatch.StartNew();
            var ch = new DtlsDatagramChannel(ep.Host, ep.Port, alloc.AllocationId, alloc.Key);
            Console.WriteLine($"  DTLS handshake with {ep.Host}:{ep.Port} ok in {sw.ElapsedMilliseconds} ms, cipher {ch.NegotiatedCipher}");
            return ch;
        }

        static void Describe(string who, RelayAllocation al)
        {
            Span<byte> mine = stackalloc byte[16];
            GuidCodec.Write(al.AllocationId, mine);
            bool idMatches = mine.SequenceEqual(al.AllocationIdBytes);
            Console.WriteLine($"  {who}: allocation {al.AllocationId} region {al.Region} " +
                              $"connData {al.ConnectionData.Length} B key {al.Key.Length} B " +
                              (al.HostConnectionData != null ? $"hostConnData {al.HostConnectionData.Length} B " : "") +
                              $"idBytes=RFC4122:{idMatches} endpoints [{string.Join(", ", al.Endpoints.Select(e => $"{e.ConnectionType} {e.Host}:{e.Port}"))}]");
        }

        static async Task<int> SelfTest(UgsClient ugs, Args a)
        {
            bool dtls = a.Has("--dtls");
            int count = a.GetInt("--count", 200), rate = a.GetInt("--rate", 30);
            var sw = Stopwatch.StartNew();

            Console.WriteLine("1. Anonymous sign-in (two players)");
            var hostP = await ugs.SignInAnonymouslyAsync();
            var joinP = await ugs.SignInAnonymouslyAsync();
            Console.WriteLine($"  host player {hostP.PlayerId}, joiner player {joinP.PlayerId}  ({sw.ElapsedMilliseconds} ms)");

            Console.WriteLine("2. Relay allocation, join code, join");
            var t = Stopwatch.StartNew();
            var hostAlloc = await ugs.AllocateAsync(hostP, maxConnections: 3, region: a.GetOrNull("--region"));
            var code = await ugs.CreateJoinCodeAsync(hostP, hostAlloc.AllocationId);
            var joinAlloc = await ugs.JoinAsync(joinP, code);
            Console.WriteLine($"  join code {code}  ({t.ElapsedMilliseconds} ms for allocate+joincode+join)");
            Describe("host  ", hostAlloc);
            Describe("joiner", joinAlloc);
            bool hostDataMatches = joinAlloc.HostConnectionData!.AsSpan().SequenceEqual(hostAlloc.ConnectionData);
            Console.WriteLine($"  joiner's hostConnectionData == host's connectionData: {hostDataMatches}");

            Console.WriteLine($"3. Relay protocol over {(dtls ? "DTLS" : "plain UDP")}");
            using var host = new RelayConnection("host", hostAlloc, Open(hostAlloc, dtls)) { Log = Console.WriteLine };
            using var join = new RelayConnection("joiner", joinAlloc, Open(joinAlloc, dtls)) { Log = Console.WriteLine };
            Console.WriteLine($"  host BIND ok in {host.Bind().TotalMilliseconds:F0} ms");
            Console.WriteLine($"  joiner BIND ok in {join.Bind().TotalMilliseconds:F0} ms");
            var hostId = join.Connect(joinAlloc.HostConnectionData!);
            Console.WriteLine($"  joiner CONNECT_REQUEST -> ACCEPTED, host id {hostId} (== host allocation: {hostId == hostAlloc.AllocationId})");

            // Host: echo every request; remember who connected (it learns peers from RELAY traffic).
            var hostPeers = new ConcurrentDictionary<Guid, bool>();
            var hostRtt = new Rtt();
            host.Data += (from, payload) =>
            {
                hostPeers[from] = true;
                if (payload.Length >= EchoHeader && payload[0] is EchoRequest or ProbeRequest) Reply(host, from, payload);
                else if (payload.Length >= EchoHeader && payload[0] == EchoReply) hostRtt.Got(payload);
            };
            var joinRtt = new Rtt();
            join.Data += (from, payload) =>
            {
                if (payload.Length >= EchoHeader && payload[0] == EchoRequest) Reply(join, from, payload);
                else if (payload.Length >= EchoHeader && payload[0] == EchoReply) joinRtt.Got(payload);
            };

            Console.WriteLine($"4. Echo traffic: {count} messages each way at {rate} Hz, 64-byte payloads");
            var joinerTask = Task.Run(() => Pump(join, hostId, count, rate, 64, joinRtt));
            // Host starts once it has heard from the joiner (that is how it learns the joiner's id).
            var wait = Stopwatch.StartNew();
            while (hostPeers.IsEmpty && wait.ElapsedMilliseconds < 5000) await Task.Delay(10);
            if (hostPeers.IsEmpty) throw new TimeoutException("host never received a RELAY message from the joiner");
            var joinerId = hostPeers.Keys.First();
            Console.WriteLine($"  host learned joiner id {joinerId} from its first RELAY (== joiner allocation: {joinerId == joinAlloc.AllocationId})");
            var hostTask = Task.Run(() => Pump(host, joinerId, count, rate, 64, hostRtt));
            await Task.WhenAll(joinerTask, hostTask);
            await Task.Delay(1000); // late replies

            Console.WriteLine("5. Payload size sweep (5 probes per size; the doc'd server cap is 1400 B content = 1438 B datagram)");
            var sizes = new[] { 256, 1024, 1300, 1390, 1391, 1392, 1393, 1394, 1395, 1396, 1397, 1398, 1399, 1400 };
            var arrived = new ConcurrentDictionary<int, int>();
            join.Data += (from, payload) => { if (payload.Length >= EchoHeader && payload[0] == ProbeReply) arrived.AddOrUpdate(payload.Length, 1, (_, n) => n + 1); };
            foreach (var size in sizes)
            {
                var probe = new byte[size];
                for (int i = 0; i < 5; i++) { probe[0] = ProbeRequest; BinaryPrimitives.WriteUInt32BigEndian(probe.AsSpan(1), 100000u + (uint)i); BinaryPrimitives.WriteInt64BigEndian(probe.AsSpan(5), Stopwatch.GetTimestamp()); join.SendTo(hostId, probe); await Task.Delay(30); }
            }
            await Task.Delay(1000);
            Console.WriteLine("  " + string.Join("  ", sizes.Select(s => $"{s}B(+38)={arrived.GetValueOrDefault(s)}/5")));
            try { join.SendTo(hostId, new byte[1401]); Console.WriteLine("  1401 B: sent (unexpected)"); }
            catch (ArgumentException) { Console.WriteLine("  1401 B: refused locally (protocol cap 1400)"); }

            Console.WriteLine("6. Results");
            joinRtt.Print("joiner->host->joiner", count);
            hostRtt.Print("host->joiner->host  ", count);
            Report("Relay PING (joiner<->server)", join.ServerRtts());
            Report("Relay PING (host<->server)  ", host.ServerRtts());
            Console.WriteLine($"  datagrams: host sent {host.DatagramsSent} recv {host.DatagramsReceived}; joiner sent {join.DatagramsSent} recv {join.DatagramsReceived}; relay errors host {host.Errors} joiner {join.Errors}");

            Console.WriteLine("7. Disconnect + close");
            join.Disconnect(hostId);
            await Task.Delay(300);
            join.Close(); host.Close();
            Console.WriteLine($"  done in {sw.Elapsed.TotalSeconds:F1} s (allocations expire on their own)");
            return joinRtt.Received > 0 && hostRtt.Received > 0 ? 0 : 1;
        }

        /// <summary>Handshake-only: offer each PSK suite alone and report which ones the Relay server accepts.</summary>
        static async Task<int> DtlsProbe(UgsClient ugs)
        {
            var p = await ugs.SignInAnonymouslyAsync();
            var alloc = await ugs.AllocateAsync(p, 1);
            var ep = alloc.Endpoint("dtls");
            Console.WriteLine($"  probing {ep.Host}:{ep.Port} with each suite alone");
            int ok = 0;
            foreach (var suite in DtlsDatagramChannel.OfferedSuites)
            {
                string name = DtlsDatagramChannel.RelayPskClient.CipherSuiteName(suite);
                try
                {
                    using var ch = new DtlsDatagramChannel(ep.Host, ep.Port, alloc.AllocationId, alloc.Key, 5000, new[] { suite });
                    Console.WriteLine($"  ACCEPTED  {name}");
                    ok++;
                }
                catch (Exception e) { Console.WriteLine($"  rejected  {name}: {e.GetType().Name} {e.Message}"); }
            }
            return ok > 0 ? 0 : 1;
        }

        static async Task<int> Host(UgsClient ugs, Args a)
        {
            bool dtls = a.Has("--dtls");
            int seconds = a.GetInt("--seconds", 120);
            var p = await ugs.SignInAnonymouslyAsync();
            var alloc = await ugs.AllocateAsync(p, 3, a.GetOrNull("--region"));
            var code = await ugs.CreateJoinCodeAsync(p, alloc.AllocationId);
            Describe("host", alloc);
            using var host = new RelayConnection("host", alloc, Open(alloc, dtls)) { Log = Console.WriteLine };
            host.Bind();
            Console.WriteLine($"JOIN CODE: {code}   (echoing for {seconds} s)");
            var peers = new ConcurrentDictionary<Guid, long>();
            host.Data += (from, payload) =>
            {
                if (peers.TryAdd(from, 0)) Console.WriteLine($"  peer {from} connected");
                peers[from]++;
                if (payload.Length >= EchoHeader && payload[0] == EchoRequest) Reply(host, from, payload);
            };
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            foreach (var kv in peers) Console.WriteLine($"  {kv.Key}: {kv.Value} messages");
            Report("Relay PING (host<->server)", host.ServerRtts());
            return 0;
        }

        static async Task<int> Join(UgsClient ugs, Args a, string code)
        {
            bool dtls = a.Has("--dtls");
            int count = a.GetInt("--count", 300), rate = a.GetInt("--rate", 30);
            var p = await ugs.SignInAnonymouslyAsync();
            var alloc = await ugs.JoinAsync(p, code);
            Describe("joiner", alloc);
            using var join = new RelayConnection("joiner", alloc, Open(alloc, dtls)) { Log = Console.WriteLine };
            join.Bind();
            var hostId = join.Connect(alloc.HostConnectionData!);
            var rtt = new Rtt();
            join.Data += (from, payload) => { if (payload.Length >= EchoHeader && payload[0] == EchoReply) rtt.Got(payload); };
            Pump(join, hostId, count, rate, 64, rtt);
            await Task.Delay(1000);
            rtt.Print("joiner->host->joiner", count);
            Report("Relay PING (joiner<->server)", join.ServerRtts());
            join.Disconnect(hostId);
            return rtt.Received > 0 ? 0 : 1;
        }

        static void Reply(RelayConnection c, Guid to, ReadOnlySpan<byte> request)
        {
            Span<byte> reply = stackalloc byte[request.Length];
            request.CopyTo(reply);
            reply[0] = (byte)(request[0] + 1); // request kind -> its reply kind
            c.SendTo(to, reply);
        }

        static void Pump(RelayConnection c, Guid to, int count, int rate, int size, Rtt rtt)
        {
            var buf = new byte[Math.Max(size, EchoHeader)];
            long interval = Stopwatch.Frequency / Math.Max(1, rate);
            long next = Stopwatch.GetTimestamp();
            for (uint seq = 0; seq < count; seq++)
            {
                while (Stopwatch.GetTimestamp() < next) Thread.Sleep(1);
                next += interval;
                buf[0] = EchoRequest;
                BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(1), seq);
                BinaryPrimitives.WriteInt64BigEndian(buf.AsSpan(5), Stopwatch.GetTimestamp());
                c.SendTo(to, buf);
                rtt.Sent++;
            }
        }

        static void Report(string name, double[] ms)
        {
            if (ms.Length == 0) { Console.WriteLine($"  {name}: no samples"); return; }
            Array.Sort(ms);
            Console.WriteLine($"  {name}: n={ms.Length} min {ms[0]:F1} p50 {Pct(ms, 50):F1} p95 {Pct(ms, 95):F1} max {ms[^1]:F1} ms");
        }

        static double Pct(double[] sorted, double p) => sorted[Math.Clamp((int)Math.Ceiling(p / 100 * sorted.Length) - 1, 0, sorted.Length - 1)];

        sealed class Rtt
        {
            readonly ConcurrentBag<double> _ms = new();
            readonly ConcurrentDictionary<uint, bool> _seen = new();
            public int Sent;
            public int Duplicates;
            public int Received => _seen.Count;
            public void Got(ReadOnlySpan<byte> reply)
            {
                uint seq = BinaryPrimitives.ReadUInt32BigEndian(reply[1..]);
                long t0 = BinaryPrimitives.ReadInt64BigEndian(reply[5..]);
                if (!_seen.TryAdd(seq, true)) { Interlocked.Increment(ref Duplicates); return; }
                _ms.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
            public void Print(string name, int expected)
            {
                var ms = _ms.ToArray();
                Report($"{name} RTT", ms);
                Console.WriteLine($"  {name} delivered {Received}/{expected} ({100.0 * Received / Math.Max(1, expected):F1} %), duplicates {Duplicates}");
            }
        }

        sealed class Args
        {
            readonly List<string> _pos = new();
            readonly Dictionary<string, string?> _opt = new();
            public Args(string[] a)
            {
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i].StartsWith("--"))
                    {
                        string key = a[i];
                        _opt[key] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : null;
                    }
                    else _pos.Add(a[i]);
                }
            }
            public string? Positional(int i) => i < _pos.Count ? _pos[i] : null;
            public bool Has(string k) => _opt.ContainsKey(k);
            public string Get(string k, string d) => _opt.TryGetValue(k, out var v) && v != null ? v : d;
            public string? GetOrNull(string k) => _opt.TryGetValue(k, out var v) ? v : null;
            public int GetInt(string k, int d) => _opt.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : d;
        }
    }
}
