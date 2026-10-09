using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>One benchmark run of one scene: what its session report measured.</summary>
    public sealed class BenchResult
    {
        public string Scene { get; set; } = "";
        public int Run { get; set; }
        /// <summary>"" the default GC mode, "low" SustainedLowLatency (--gc-latency low).</summary>
        public string Gc { get; set; } = "";
        public bool Ok { get; set; }
        public string? Problem { get; set; }
        public double Seconds { get; set; }
        public int Frames { get; set; }
        public double P50Ms { get; set; }
        public double P95Ms { get; set; }
        public double P99Ms { get; set; }
        public double WorstMs { get; set; }
        public int Over33 { get; set; }
        public double SimP50Ms { get; set; }
        public double SimP95Ms { get; set; }
        public double RenderP50Ms { get; set; }
        /// <summary>GPU time per frame from timer queries; null when the driver gave none (headless, GLES).</summary>
        public double? GpuP50Ms { get; set; }
        /// <summary>Seconds the scene took to load, to its first frame (older reports: when it started running).</summary>
        public double LoadSec { get; set; }
        /// <summary>Process start to the first frame (runtime, content boot, first scene).</summary>
        public double BootSec { get; set; }
        public double HeapMB { get; set; }
        public double KbPerFrameP95 { get; set; }
        public double GcPauseMsPerFrame { get; set; }
        public int Exceptions { get; set; }
        public int Errors { get; set; }
        public string Report { get; set; } = "";
    }

    /// <summary>A TIME page benchmark: its settings, machine, commit and every run's results.</summary>
    public sealed class BenchSession
    {
        public DateTime Started { get; set; }
        public string Branch { get; set; } = "";
        public string Commit { get; set; } = "";
        public string Machine { get; set; } = "";
        public string Gpu { get; set; } = "";
        public int Frames { get; set; }
        public bool Headless { get; set; }
        public bool VSync { get; set; }
        public string Size { get; set; } = "";
        /// <summary>0 default GC, 1 low latency, 2 A/B (both).</summary>
        public int GcMode { get; set; }
        public List<BenchResult> Results { get; set; } = new();
        public string File { get; set; } = "";
    }

    public sealed partial class LauncherJobs
    {
        public static string BenchDir => Path.Combine(LauncherSettings.DataDir, "bench");

        /// <summary>
        /// The parity replays (Port/parity/manifest.json) as benchmark items, "replay:NAME": recorded
        /// input from Bootstrap through the menu into a match, so the run times real play rather than
        /// an empty scene entered directly.
        /// </summary>
        public List<string> BenchReplays()
        {
            var manifest = Path.Combine(_ws.Dir, "Port", "parity", "manifest.json");
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                return doc.RootElement.GetProperty("cases").EnumerateArray()
                    .Select(c => "replay:" + c.GetProperty("name").GetString()).ToList();
            }
            catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { return new List<string>(); }
        }

        /// <summary>The benchmark running or last run (results fill in as each run closes).</summary>
        public BenchSession? Bench { get; private set; }

        /// <summary>Saved benchmarks, newest first.</summary>
        public static List<BenchSession> BenchHistory()
        {
            var list = new List<BenchSession>();
            if (!Directory.Exists(BenchDir)) return list;
            foreach (var f in new DirectoryInfo(BenchDir).GetFiles("bench-*.json").OrderByDescending(f => f.Name).Take(30))
                try
                {
                    var b = JsonSerializer.Deserialize<BenchSession>(File.ReadAllText(f.FullName));
                    if (b != null) { b.File = f.FullName; list.Add(b); }
                }
                catch (Exception e) when (e is JsonException or IOException) { }
            return list;
        }

        /// <summary>
        /// TIME > BENCHMARK: builds the Release player, then runs every scene (each run its own
        /// game process, entered directly, for a fixed number of frames with vsync off by default)
        /// and closes it when the frames are done. Each run's session report becomes a row; the
        /// session is saved under bench/ so the next run can be compared with it.
        /// </summary>
        public void Benchmark(IReadOnlyList<string> scenes, int frames, int runs, bool headless, bool vsync, string size, int gcMode = 0) => Start("Benchmark", async ct =>
        {
            if (scenes.Count == 0) { Log.Add(LogKind.Error, "Pick at least one scene to time."); return false; }
            if (!await EnsureTools(ct)) return false;
            if (!await EnsurePlayerBuilt(ct, "Release")) return false;
            var exe = PlayerExeFor("Release");
            Directory.CreateDirectory(BenchDir);
            var stamp = DateTime.Now;
            var session = new BenchSession
            {
                Started = stamp, Branch = _s.Branch, Commit = Commit?.Sha ?? "", Machine = Environment.MachineName,
                Frames = frames, Headless = headless, VSync = vsync, Size = size, GcMode = gcMode,
                File = Path.Combine(BenchDir, $"bench-{stamp:yyyyMMdd-HHmmss}.json"),
            };
            Bench = session;
            var gcs = gcMode switch { 1 => new[] { "low" }, 2 => new[] { "", "low" }, _ => new[] { "" } };
            int total = scenes.Count * runs * gcs.Length, done = 0;
            foreach (var scene in scenes)
                for (int run = 1; run <= runs; run++)
                foreach (var gc in gcs)
                {
                    ct.ThrowIfCancellationRequested();
                    Step($"Timing {scene} ({run}/{runs}{(gc == "low" ? ", low-latency GC" : "")})", (float)done / total);
                    var report = Path.Combine(BenchDir, $"run-{stamp:yyyyMMdd-HHmmss}-{Safe(scene)}-{run}{(gc == "" ? "" : "-" + gc)}.json");
                    var args = new List<string> { "--session-report", report };
                    if (gc == "low") { args.Add("--gc-latency"); args.Add("low"); }
                    if (scene.StartsWith("replay:", StringComparison.Ordinal))
                    {
                        // A replay sets its own scene, seed and length.
                        var name = scene["replay:".Length..];
                        args.AddRange(new[] { "--replay", Path.Combine(_ws.Dir, "Port", "parity", "replays", name + ".json"),
                                              "--parity-out", Path.Combine(BenchDir, "parity-" + Safe(name)) });
                    }
                    else args.AddRange(new[] { "--scene", scene, "--frames", frames.ToString() });
                    if (headless) args.Add("--headless");
                    else { args.Add("--size"); args.Add(size); }
                    if (!vsync) args.Add("--no-vsync");
                    // PLAY's own save slot: it has already been through the login prompts, so a replay
                    // gets from Bootstrap into its match (a fresh slot stops at the prompts).
                    var psi = PlayerStart(exe, args, audio: false, network: false, profile: string.IsNullOrWhiteSpace(_s.Profile) ? null : _s.Profile.Trim());
                    var result = new BenchResult { Scene = scene, Run = run, Gc = gc, Report = report };
                    var sw = Stopwatch.StartNew();
                    using (var p = Process.Start(psi)!)
                    {
                        p.OutputDataReceived += (_, e) => { };
                        p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, "[bench] " + e.Data); };
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                        // Generous: software GL can run a few frames a second.
                        var limit = TimeSpan.FromSeconds(120 + Math.Max(frames, 6000) / 10.0);
                        while (!p.WaitForExit(250))
                        {
                            if (ct.IsCancellationRequested || sw.Elapsed > limit)
                            {
                                try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                                result.Problem = ct.IsCancellationRequested ? "cancelled" : $"did not finish in {limit.TotalSeconds:0} s";
                                break;
                            }
                        }
                    }
                    ReadReport(result);
                    session.Results.Add(result);
                    done++;
                    Log.Add(result.Ok ? LogKind.Info : LogKind.Warn,
                        $"{scene} run {run}: " + (result.Ok ? $"p50 {result.P50Ms:0.0} ms, p95 {result.P95Ms:0.0} ms, load {result.LoadSec:0.0} s" : result.Problem));
                    if (ct.IsCancellationRequested) break;
                }
            session.Gpu = GpuOf(session.Results.FirstOrDefault()?.Report);
            File.WriteAllText(session.File, JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
            Log.Add(LogKind.Success, $"Benchmark saved: {session.File}");
            return session.Results.Any(r => r.Ok);
        });

        static string Safe(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));

        static void ReadReport(BenchResult r)
        {
            if (!File.Exists(r.Report)) { r.Problem ??= "the game wrote no report (it crashed or never started)"; return; }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(r.Report));
                var d = doc.RootElement;
                double N(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
                r.Seconds = N(d, "seconds");
                if (d.TryGetProperty("frames", out var f))
                {
                    r.Frames = (int)N(f, "presented"); r.P50Ms = N(f, "p50Ms"); r.P95Ms = N(f, "p95Ms");
                    r.P99Ms = N(f, "p99Ms"); r.WorstMs = N(f, "worstMs"); r.Over33 = (int)N(f, "over33Ms");
                }
                if (d.TryGetProperty("cpu", out var c)) { r.SimP50Ms = N(c, "simP50Ms"); r.SimP95Ms = N(c, "simP95Ms"); r.RenderP50Ms = N(c, "renderP50Ms"); }
                if (d.TryGetProperty("gpu", out var g) && g.TryGetProperty("timerQueries", out var tq) && tq.ValueKind == JsonValueKind.True && N(g, "frames") > 0)
                    r.GpuP50Ms = N(g, "p50Ms");
                if (d.TryGetProperty("memory", out var m)) { r.HeapMB = N(m, "heapMB"); r.KbPerFrameP95 = N(m, "kbPerFrameP95"); r.GcPauseMsPerFrame = N(m, "steadyGcPauseMsPerFrame"); }
                r.BootSec = N(d, "bootMs") / 1000;
                if (d.TryGetProperty("scenes", out var sc) && sc.ValueKind == JsonValueKind.Array && sc.GetArrayLength() > 0)
                {
                    // The scene asked for; for a replay (Bootstrap, menu, match) the one it played longest.
                    var list = sc.EnumerateArray().ToList();
                    var timed = list.FirstOrDefault(x => x.TryGetProperty("name", out var nm) && nm.GetString() == r.Scene);
                    if (timed.ValueKind != JsonValueKind.Object) timed = list.OrderByDescending(x => N(x, "seconds")).First();
                    // loadMs where the player measures it; else when the scene started running.
                    r.LoadSec = timed.TryGetProperty("loadMs", out var lm) && lm.ValueKind == JsonValueKind.Number ? lm.GetDouble() / 1000 : N(timed, "enteredAtSecond");
                }
                if (d.TryGetProperty("counts", out var counts)) { r.Exceptions = (int)N(counts, "exceptions"); r.Errors = (int)N(counts, "errors"); }
                bool crashed = d.TryGetProperty("crash", out var cr) && cr.ValueKind == JsonValueKind.String;
                if (crashed) r.Problem = "crashed: " + cr.GetString();
                r.Ok = !crashed && r.Frames > 0 && r.Problem == null;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidOperationException)
            {
                r.Problem ??= "unreadable report: " + e.Message;
            }
        }

        static string GpuOf(string? report)
        {
            try
            {
                if (report == null || !File.Exists(report)) return "";
                using var doc = JsonDocument.Parse(File.ReadAllText(report));
                return doc.RootElement.TryGetProperty("machine", out var m) && m.TryGetProperty("gpu", out var g) ? g.GetString() ?? "" : "";
            }
            catch (Exception e) when (e is JsonException or IOException) { return ""; }
        }

        // ---------------------------------------------------------------- local multiplayer

        readonly List<(int player, Process proc, int port)> _locals = new();

        /// <summary>The local multiplayer instances still running.</summary>
        public int LocalPlayersRunning { get { lock (_locals) return _locals.Count(l => !l.proc.HasExited); } }

        /// <summary>Every local player of the current run: its number, control port and whether it still runs.</summary>
        public List<(int player, int port, bool running)> LocalPlayers { get { lock (_locals) return _locals.Select(l => (l.player, l.port, !l.proc.HasExited)).ToList(); } }

        /// <summary>The session folder the current local run's players share (the stand-in for UGS Lobby + Relay).</summary>
        public string? LocalNetDir { get; private set; }

        /// <summary>The relay server a RELAY run started beside its players, and the URL they use; null otherwise.</summary>
        Process? _localRelay;
        public string? LocalRelayUrl { get; private set; }

        /// <summary>
        /// NET > PLAYERS: N game windows on this machine (a party is 4, so 2-4), each its own player
        /// (profile player1..N, so each has its own save and name), networking on, tiled 2x2. They
        /// find each other the way PCs on a LAN do, through one fresh session folder, so one hosts a
        /// party or match and the others join it from the game's own menus. Each has a control port
        /// the NET page drives (stats, network simulator, session faults) and may start on a
        /// simulated line (<paramref name="sims"/>, docs/MULTIPLAYER.md §6.2). Only player 1 plays
        /// sound. Each writes a session report under sessions/. <paramref name="transport"/> "relay"
        /// starts Froglet's relay server first and sends every player's sessions through it (§6.7);
        /// "ugs" sends them through UGS Relay, each player signed in to the live project as its profile (§6.8).
        /// </summary>
        public void LaunchLocalPlayers(int players, string? scene, string size, IReadOnlyList<string>? sims = null, string? transport = null) => Start("Local multiplayer", async ct =>
        {
            players = Math.Clamp(players, 2, Prisma.MultiplayerRun.MaxPlayers);
            if (!await EnsureTools(ct)) return false;
            string cfg = _s.ReleaseBuild ? "Release" : "Debug";
            if (!await EnsurePlayerBuilt(ct, cfg)) return false;
            StopLocalPlayers();
            Step($"Starting {players} players", 1);
            var exe = PlayerExeFor(cfg);
            bool audio = _s.Audio && await _ws.FetchNatives(Log, ct);
            // A fresh session folder per run: no session a previous run left behind shows up as joinable.
            LocalNetDir = Path.Combine(Path.GetTempPath(), "prisma-multiplayer", DateTime.Now.ToString("yyyyMMdd-HHmmss"), "sessions");
            Directory.CreateDirectory(LocalNetDir);
            bool relay = transport == "relay", ugs = transport == "ugs";
            if (ugs)
            {
                Log.Add(LogKind.Info, "Every player goes through UGS Relay, signed in to the game's live UGS project as its profile.");
                transport = "udp";
            }
            if (relay)
            {
                Step("Starting Froglet's relay server", 1);
                LocalRelayUrl = await StartLocalRelay(exe, ct);
                if (LocalRelayUrl == null)
                {
                    StopLocalPlayers(); // and the relay, if it is up but never printed its address
                    Log.Add(LogKind.Error, "The relay server did not start (see its [relay] lines above).");
                    return false;
                }
                Log.Add(LogKind.Info, $"Every player goes through the relay at {LocalRelayUrl}: hosts allocate, joiners join by code.");
                transport = "udp";
            }
            var wh = size.Split('x');
            int w = wh.Length == 2 && int.TryParse(wh[0], out var pw) ? pw : 960, h = wh.Length == 2 && int.TryParse(wh[1], out var ph) ? ph : 540;
            for (int i = 1; i <= players; i++)
            {
                int port = FreeLocalPort();
                var args = new List<string> { "--size", size, "--control-port", port.ToString(), "--position", $"{(i - 1) % 2 * w},{(i - 1) / 2 * (h + 32)}" };
                if (!string.IsNullOrWhiteSpace(scene)) { args.Add("--scene"); args.Add(scene!); }
                var report = Path.Combine(SessionsDir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}-p{i}.json");
                Directory.CreateDirectory(SessionsDir);
                args.Add("--session-report"); args.Add(report);
                var psi = PlayerStart(exe, args, audio: audio && i == 1, network: true, profile: "player" + i);
                psi.Environment["COSMIC_SHORE_NET_DIR"] = LocalNetDir;
                if (!string.IsNullOrWhiteSpace(transport)) psi.Environment["COSMIC_SHORE_NET_TRANSPORT"] = transport;
                if (relay) psi.Environment["COSMIC_SHORE_RELAY"] = LocalRelayUrl!;
                else if (ugs) psi.Environment["COSMIC_SHORE_RELAY"] = "ugs";
                else psi.Environment.Remove("COSMIC_SHORE_RELAY");
                var sim = sims != null && i - 1 < sims.Count ? sims[i - 1] : "";
                if (!string.IsNullOrWhiteSpace(sim)) psi.Environment["COSMIC_SHORE_NET_SIM"] = sim;
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                int n = i;
                p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, $"[P{n}] {e.Data}"); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, $"[P{n}] {e.Data}"); };
                p.Exited += (_, _) => Log.Add(LogKind.Info, $"Player {n} closed.");
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                lock (_locals) _locals.Add((i, p, port));
                // A moment apart, so player 1 is up first and the windows do not all fight for the GPU at once.
                await Task.Delay(1500, ct);
            }
            Log.Add(LogKind.Success, $"{players} players running. Host a party or match in one window and join it from the others.");
            return true;
        });

        static int FreeLocalPort()
        {
            var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            l.Start();
            int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void StopLocalPlayers()
        {
            lock (_locals)
            {
                foreach (var (_, p, _) in _locals)
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                _locals.Clear();
                try { if (_localRelay is { HasExited: false }) _localRelay.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                _localRelay = null;
                LocalRelayUrl = null;
            }
        }

        /// <summary>
        /// NET > UGS RELAY CHECK: <c>CosmicShore --ugs-relay-check</c> (docs/MULTIPLAYER.md §6.8). Signs in two UGS
        /// players in the game's live project (the same two every time: their session tokens stay in the
        /// "relaycheck" profile), allocates, joins by code, connects through UGS Relay and times frames both ways.
        /// </summary>
        public void RunUgsRelayCheck() => Start("UGS relay check", async ct =>
        {
            if (!await EnsureTools(ct)) return false;
            string cfg = _s.ReleaseBuild ? "Release" : "Debug";
            if (!await EnsurePlayerBuilt(ct, cfg)) return false;
            Step("Signing in to UGS and going through UGS Relay", 1);
            var p = new Process { StartInfo = PlayerStart(PlayerExeFor(cfg), new[] { "--ugs-relay-check" }, audio: false, network: true, profile: "relaycheck"), EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using (ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }))
                await p.WaitForExitAsync(CancellationToken.None);
            bool ok = p.ExitCode == 0;
            Log.Add(ok ? LogKind.Success : LogKind.Error, ok ? "UGS Relay works from Prisma." : "The UGS relay check failed; the [relay-check] lines above name the step.");
            return ok;
        });

        /// <summary>Starts <c>CosmicShore --relay-server</c> and returns the URL it prints, or null when it does not come up.</summary>
        async Task<string?> StartLocalRelay(string exe, CancellationToken ct)
        {
            const string marker = "COSMIC_SHORE_RELAY=";
            var url = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var p = new Process { StartInfo = PlayerStart(exe, new[] { "--relay-server", "0", "0" }, audio: false, network: true, profile: null), EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Log.Add(LogKind.Output, e.Data);
                int at = e.Data.IndexOf(marker, StringComparison.Ordinal);
                if (at >= 0) url.TrySetResult(e.Data[(at + marker.Length)..].TrimEnd(')', ' '));
            };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            p.Exited += (_, _) => { url.TrySetResult(null); Log.Add(LogKind.Info, "The relay server closed."); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_locals) _localRelay = p;
            var done = await Task.WhenAny(url.Task, Task.Delay(TimeSpan.FromSeconds(30), ct));
            return done == url.Task ? url.Task.Result : null;
        }

        /// <summary>How the launcher starts any game process: the workspace's project, its .NET, and the run's choices.</summary>
        ProcessStartInfo PlayerStart(string exe, IEnumerable<string> args, bool audio, bool network, string? profile)
        {
            var psi = new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.Combine(_ws.Dir, "Port"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            foreach (var kv in _tools.DotnetEnv()) psi.Environment[kv.Key] = kv.Value;
            psi.Environment["COSMIC_SHORE_PROJECT"] = _ws.Dir;
            if (!audio) psi.Environment["COSMIC_SHORE_AUDIO"] = "off";
            if (!network) psi.Environment["COSMIC_SHORE_NET"] = "off";
            if (_s.MobileRenderPath) psi.Environment["COSMIC_SHORE_GLES"] = "1";
            if (!string.IsNullOrWhiteSpace(profile)) psi.Environment["COSMIC_SHORE_PROFILE"] = profile;
            psi.Environment["COSMIC_SHORE_BRANCH"] = _s.Branch;
            return psi;
        }
    }
}
