using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Online
{
    /// <summary>
    /// Picks the Relay region with the lowest round trip from this machine. The QoS Discovery service lists
    /// each region's QoS servers; every region is measured in parallel and the lowest median wins:
    ///  • first with the QoS protocol on UDP (the servers' port, 7778): five requests per region, each
    ///    echoed by the server - written from Unity's public protocol page
    ///    (https://docs.unity.com/clanforge/legacy/quality-of-service-overview);
    ///  • only if no region answered that (a firewall dropping unknown UDP), an ICMP ping (three tries).
    /// When nothing answers at all, the choice is left to Relay, which then uses its default region.
    /// </summary>
    public static class RelayRegions
    {
        public sealed record Measurement(string Region, double MedianMs)
        {
            public override string ToString() => $"{Region} {MedianMs:0} ms";
        }

        /// <summary>Measures one QoS endpoint ("host:port"), median ms; -1 when it does not answer. Tests swap this.</summary>
        internal static Func<string, Task<double>> Measure = endpoint => { var (h, p) = Split(endpoint); return QosAsync(h, p); };
        /// <summary>The fallback measurement (ICMP), used only when no QoS server answered. Tests swap this.</summary>
        internal static Func<string, Task<double>> Fallback = PingEndpointAsync;

        const byte QosRequest = 0x59, QosResponse = 0x95;
        static readonly byte[] Title = System.Text.Encoding.UTF8.GetBytes("cosmicshore-prisma");

        public static async Task<(string region, IReadOnlyList<Measurement> measured)> PickAsync(IUgsApi api, UgsPlayer player, CancellationToken ct = default)
        {
            IReadOnlyList<QosServer> servers;
            try { servers = await api.ListQosServersAsync(player, ct).ConfigureAwait(false); }
            catch (Exception e) when (e is UgsException or System.Net.Http.HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"[relay] region discovery failed ({e.Message}); leaving the region to Relay");
                return (null, Array.Empty<Measurement>());
            }
            var regions = servers.Where(s => s.Endpoints.Count > 0).GroupBy(s => s.Region)
                .Select(g => (region: g.Key, endpoint: Preferred(g.First().Endpoints))).ToList();
            var results = await MeasureAllAsync(regions, Measure).ConfigureAwait(false);
            if (results.Count == 0) results = await MeasureAllAsync(regions, Fallback).ConfigureAwait(false);
            return (results.Count > 0 ? results[0].Region : null, results);
        }

        static async Task<List<Measurement>> MeasureAllAsync(List<(string region, string endpoint)> regions, Func<string, Task<double>> measure)
        {
            // Task.Run: a measurement may block its thread while it waits for replies; run the regions in parallel.
            var tasks = regions.Select(async r => new Measurement(r.region, await Task.Run(() => measure(r.endpoint)).ConfigureAwait(false)));
            return (await Task.WhenAll(tasks).ConfigureAwait(false)).Where(m => m.MedianMs >= 0).OrderBy(m => m.MedianMs).ToList();
        }

        /// <summary>The IPv4 endpoint when there is one (most home lines route IPv4 best), else the first.</summary>
        internal static string Preferred(IReadOnlyList<string> endpoints)
            => endpoints.FirstOrDefault(e => !e.StartsWith("[", StringComparison.Ordinal)) ?? endpoints[0];

        /// <summary>"1.2.3.4:7778" → ("1.2.3.4", 7778); "[::1]:7778" → ("::1", 7778).</summary>
        internal static (string host, int port) Split(string endpoint)
        {
            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                int close = endpoint.IndexOf(']');
                string h = close > 0 ? endpoint[1..close] : endpoint;
                return (h, close > 0 && close + 2 <= endpoint.Length && int.TryParse(endpoint[(close + 2)..], out var p6) ? p6 : 7778);
            }
            int colon = endpoint.LastIndexOf(':');
            return colon > 0 && int.TryParse(endpoint[(colon + 1)..], out var p) ? (endpoint[..colon], p) : (endpoint, 7778);
        }

        /// <summary>A QoS request: [0x59][version 0 | flow 0][title length incl. itself][title][sequence u8][identifier u16][timestamp u64].</summary>
        internal static int WriteQosRequest(Span<byte> dst, byte sequence, ushort identifier, ulong timestampMs)
        {
            int n = 0;
            dst[n++] = QosRequest;
            dst[n++] = 0;
            dst[n++] = (byte)(Title.Length + 1);
            Title.CopyTo(dst[n..]); n += Title.Length;
            dst[n++] = sequence;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(dst[n..], identifier); n += 2;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(dst[n..], timestampMs); n += 8;
            return n;
        }

        /// <summary>A QoS response echoes the custom data after [0x95][version | flow]: true with its sequence when it is ours.</summary>
        internal static bool TryReadQosResponse(ReadOnlySpan<byte> d, ushort identifier, out byte sequence, out byte flow)
        {
            sequence = 0; flow = 0;
            if (d.Length < 2 + 11 || d[0] != QosResponse) return false;
            flow = (byte)(d[1] & 0x0F);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(d[3..]) != identifier) return false;
            sequence = d[2];
            return true;
        }

        static async Task<double> PingEndpointAsync(string endpoint)
        {
            var (host, _) = Split(endpoint);
            var samples = new List<double>();
            for (int i = 0; i < 3; i++) { double ms = await PingOnceAsync(host, 1000).ConfigureAwait(false); if (ms >= 0) samples.Add(ms); }
            return Median(samples);
        }

        static double Median(List<double> samples)
        {
            if (samples.Count == 0) return -1;
            samples.Sort();
            return samples[samples.Count / 2];
        }

        static async Task<double> QosAsync(string host, int port, int count = 5, int timeoutMs = 600)
        {
            try
            {
                var ip = System.Net.IPAddress.TryParse(host, out var a) ? a : (await System.Net.Dns.GetHostAddressesAsync(host).ConfigureAwait(false))[0];
                using var sock = new System.Net.Sockets.Socket(ip.AddressFamily, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                sock.Connect(new System.Net.IPEndPoint(ip, port));
                ushort id = (ushort)Random.Shared.Next(1, 65535);
                var sentAt = new double[count];
                var samples = new List<double>();
                var tx = new byte[64];
                var rx = new byte[1500];
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int sent = 0;
                while (sw.ElapsedMilliseconds < timeoutMs + 50L * count && samples.Count < count)
                {
                    if (sent < count && sw.Elapsed.TotalMilliseconds >= sent * 50)
                    {
                        int n = WriteQosRequest(tx, (byte)sent, id, (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        sentAt[sent] = sw.Elapsed.TotalMilliseconds;
                        sent++;
                        try { await sock.SendAsync(tx.AsMemory(0, n)).ConfigureAwait(false); } catch (System.Net.Sockets.SocketException) { }
                    }
                    if (!sock.Poll(5000, System.Net.Sockets.SelectMode.SelectRead)) continue;
                    int got;
                    try { got = sock.Receive(rx); } catch (System.Net.Sockets.SocketException) { continue; }
                    if (!TryReadQosResponse(rx.AsSpan(0, got), id, out byte seq, out byte flow)) continue;
                    if (flow != 0) break; // the server asks us to back off: use what we have
                    if (seq < sent) samples.Add(sw.Elapsed.TotalMilliseconds - sentAt[seq]);
                }
                return Median(samples);
            }
            catch (Exception) { return -1; }
        }

        static async Task<double> PingOnceAsync(string host, int timeoutMs)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
                return reply.Status == IPStatus.Success ? Math.Max(0.1, reply.RoundtripTime) : -1;
            }
            catch (Exception) { return -1; } // no ICMP permission, no route: no answer
        }
    }
}
