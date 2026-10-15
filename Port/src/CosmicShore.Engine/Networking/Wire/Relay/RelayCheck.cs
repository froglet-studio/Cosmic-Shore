using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// One end-to-end trip through a relay service, the way a party uses it: a host allocates and gets a
    /// join code, a second player joins by that code, both bind, the joiner connects with Froglet's UDP
    /// transport inside RELAY messages, and frames go both ways while the round trip is timed
    /// (docs/MULTIPLAYER.md §6.8). <c>CosmicShore --ugs-relay-check</c> runs it against UGS; the tests run
    /// it against Froglet's relay server. It writes one line per step, so a failure names the step.
    /// </summary>
    public static class RelayCheck
    {
        public sealed class Result
        {
            public bool Passed;
            public string FailedStep;
            public string Region = "", Server = "", JoinCode = "";
            public double RttAverageMs, RttMinMs, RttMaxMs;
            public int Pings;
        }

        /// <param name="host">The host player's allocator.</param>
        /// <param name="joiner">The joining player's (a second player: one allocation cannot connect to itself).</param>
        public static async Task<Result> RunAsync(IRelayAllocator host, IRelayAllocator joiner, TextWriter log, string region = null, int pings = 10, int timeoutMs = 15000)
        {
            var r = new Result();
            string step = "allocate";
            UdpTransport server = null, client = null;
            try
            {
                var hostAlloc = await host.AllocateAsync(1, region);
                r.Region = hostAlloc.Region; r.Server = $"{hostAlloc.ServerHost}:{hostAlloc.ServerPort}";
                log.WriteLine($"[relay-check] allocated: region {(r.Region.Length > 0 ? r.Region : "?")}, relay {r.Server}, allocation {hostAlloc.AllocationId}");

                step = "join code";
                r.JoinCode = await host.CreateJoinCodeAsync(hostAlloc);
                log.WriteLine($"[relay-check] join code {r.JoinCode}");

                step = "join";
                var joinAlloc = await joiner.JoinAsync(r.JoinCode);
                log.WriteLine($"[relay-check] joined: allocation {joinAlloc.AllocationId}, relay {joinAlloc.ServerHost}:{joinAlloc.ServerPort}");

                step = "bind and connect";
                server = UdpTransport.Over(new RelayLink(hostAlloc, host: true), server: true);
                client = UdpTransport.Over(new RelayLink(joinAlloc, host: false), server: false, timeoutMs);
                var sw = Stopwatch.StartNew();
                await Next(client, NetEventKind.Connected, timeoutMs);
                int peer = (await Next(server, NetEventKind.Connected, timeoutMs)).Peer;
                log.WriteLine($"[relay-check] connected through the relay in {sw.ElapsedMilliseconds} ms");

                step = "frames both ways";
                var rtts = new double[Math.Max(1, pings)];
                for (int i = 0; i < rtts.Length; i++)
                {
                    sw.Restart();
                    client.Send(0, new[] { (byte)i });
                    var got = await Next(server, NetEventKind.Data, timeoutMs);
                    if (got.Payload.Length != 1 || got.Payload[0] != (byte)i) throw new InvalidOperationException("the host received the wrong frame");
                    server.Send(peer, got.Payload);
                    var back = await Next(client, NetEventKind.Data, timeoutMs);
                    if (back.Payload.Length != 1 || back.Payload[0] != (byte)i) throw new InvalidOperationException("the joiner received the wrong frame");
                    rtts[i] = sw.Elapsed.TotalMilliseconds;
                }
                r.Pings = rtts.Length;
                r.RttAverageMs = rtts.Average(); r.RttMinMs = rtts.Min(); r.RttMaxMs = rtts.Max();
                log.WriteLine($"[relay-check] {r.Pings} round trips: avg {r.RttAverageMs:0.0} ms, min {r.RttMinMs:0.0}, max {r.RttMaxMs:0.0}");
                r.Passed = true;
            }
            catch (Exception e)
            {
                r.FailedStep = step;
                log.WriteLine($"[relay-check] FAILED at '{step}': {e.Message}");
            }
            finally
            {
                client?.Dispose();
                server?.Dispose();
            }
            log.WriteLine(r.Passed ? "[relay-check] PASS" : "[relay-check] FAIL");
            return r;
        }

        static async Task<NetEvent> Next(INetTransport t, NetEventKind kind, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (t.Poll(out var e))
                {
                    if (e.Kind == kind) return e;
                    if (e.Kind == NetEventKind.Disconnected) throw new InvalidOperationException("the connection was refused or dropped");
                    continue;
                }
                await Task.Delay(1);
            }
            throw new TimeoutException($"no {kind} event within {timeoutMs} ms");
        }
    }
}
