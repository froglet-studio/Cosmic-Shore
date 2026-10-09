using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable

namespace Prisma
{
    /// <summary>
    /// Prisma's Multiplayer Play Mode: starts one to four players of the game as separate processes
    /// that find each other through one fresh session folder (docs/MULTIPLAYER.md §6.5). Each player
    /// has its own profile (save folder), control port, log, and optionally its own simulated line
    /// and session faults. The Launcher's MULTIPLAYER panel and the MCP <c>net_*</c> tools both drive it.
    ///
    /// A party is four, so four is the most a run starts.
    /// </summary>
    public sealed class MultiplayerRun : IDisposable
    {
        public const int MaxPlayers = 4;

        public sealed class PlayerOptions
        {
            /// <summary>Display name and save profile ("PilotA" .. "PilotD" by default). Reusing a name skips the first-run prompts.</summary>
            public string? Profile;
            /// <summary>Network simulator spec for this player (e.g. "4g"); null = none.</summary>
            public string? Sim;
            /// <summary>Session-service faults armed at launch (e.g. "ratelimit=2"); null = none.</summary>
            public string? Fault;
        }

        public sealed class Options
        {
            public int Players = 2;
            public bool Headless;
            /// <summary>Window size per player; windows are tiled two by two.</summary>
            public int Width = 960, Height = 540;
            public bool Audio;
            public bool Verbose = true;
            public string? Scene;
            /// <summary>The player executable or CosmicShore.dll (run with dotnet).</summary>
            public string? PlayerPath;
            /// <summary>The Unity project the player reads (COSMIC_SHORE_PROJECT); null = the player finds it.</summary>
            public string? ProjectRoot;
            /// <summary>The working directory for the players (nothing they write relative lands in the repo).</summary>
            public string? WorkDir;
            public List<PlayerOptions> PerPlayer = new();
            public List<string> ExtraArgs = new();
            /// <summary>The transport every player uses: "udp" (Froglet's, the player's default) or "tcp"; null = the default.</summary>
            public string? Transport;
            /// <summary>Extra environment for every player (e.g. COSMIC_SHORE_LOG_CHANNELS).</summary>
            public Dictionary<string, string> Environment = new();
        }

        public sealed class Player
        {
            public int Index;
            public string Profile = "";
            public int Port;
            public string LogPath = "";
            public Process? Process;
            public bool Running => Process is { HasExited: false };
        }

        static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(30) };
        readonly List<Player> _players = new();

        public IReadOnlyList<Player> Players => _players;
        /// <summary>The session folder every player shares (the stand-in for UGS Lobby and Relay).</summary>
        public string NetDir { get; private set; } = "";
        public string WorkDir { get; private set; } = "";
        public DateTime Started { get; private set; }

        public static string DefaultProfile(int index) => "Pilot" + (char)('A' + index);

        /// <summary>Starts the players. Throws when the options are invalid or the player is missing.</summary>
        public static MultiplayerRun Start(Options o)
        {
            if (o.Players < 1 || o.Players > MaxPlayers) throw new ArgumentException($"players must be 1-{MaxPlayers} (a party is {MaxPlayers})");
            if (string.IsNullOrEmpty(o.PlayerPath) || !File.Exists(o.PlayerPath)) throw new FileNotFoundException("the player is not built", o.PlayerPath);
            var run = new MultiplayerRun { Started = DateTime.Now };
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            run.WorkDir = o.WorkDir ?? Path.Combine(Path.GetTempPath(), "prisma-multiplayer", stamp);
            run.NetDir = Path.Combine(run.WorkDir, "sessions");
            // Everything is checked before the first process starts, so a refused run leaves nothing behind.
            var profiles = new List<string>();
            for (int i = 0; i < o.Players; i++)
            {
                var po = i < o.PerPlayer.Count ? o.PerPlayer[i] : null;
                string profile = string.IsNullOrWhiteSpace(po?.Profile) ? DefaultProfile(i) : po.Profile.Trim();
                if (profiles.Contains(profile, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException($"two players share the profile '{profile}': each needs its own save");
                profiles.Add(profile);
            }
            Directory.CreateDirectory(run.NetDir);
            for (int i = 0; i < o.Players; i++)
                run._players.Add(run.Launch(o, i, profiles[i], i < o.PerPlayer.Count ? o.PerPlayer[i] : null));
            return run;
        }

        Player Launch(Options o, int index, string profile, PlayerOptions? po)
        {
            int port = FreePort();
            var args = new List<string> { "--control-port", port.ToString() };
            if (o.Headless) { args.Add("--headless"); args.Add("--realtime"); }
            else
            {
                args.Add("--size"); args.Add($"{o.Width}x{o.Height}");
                // Tile two by two, so four players fit a 1080p desktop at 960x540.
                args.Add("--position"); args.Add($"{index % 2 * o.Width},{index / 2 * (o.Height + 32)}");
            }
            if (o.Verbose) args.Add("--verbose");
            if (!string.IsNullOrWhiteSpace(o.Scene)) { args.Add("--scene"); args.Add(o.Scene); }
            args.AddRange(o.ExtraArgs);

            bool dll = o.PlayerPath!.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var psi = new ProcessStartInfo(dll ? "dotnet" : o.PlayerPath)
            {
                WorkingDirectory = WorkDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            if (dll) psi.ArgumentList.Add(o.PlayerPath);
            foreach (var a in args) psi.ArgumentList.Add(a);
            foreach (var kv in o.Environment) psi.Environment[kv.Key] = kv.Value;
            psi.Environment["COSMIC_SHORE_PROFILE"] = profile;
            psi.Environment["COSMIC_SHORE_NET_DIR"] = NetDir;
            if (!string.IsNullOrWhiteSpace(o.Transport)) psi.Environment["COSMIC_SHORE_NET_TRANSPORT"] = o.Transport;
            psi.Environment.Remove("COSMIC_SHORE_NET"); // networking on
            if (!o.Audio) psi.Environment["COSMIC_SHORE_AUDIO"] = "off";
            if (!string.IsNullOrWhiteSpace(o.ProjectRoot)) psi.Environment["COSMIC_SHORE_PROJECT"] = o.ProjectRoot;
            if (!string.IsNullOrWhiteSpace(po?.Sim)) psi.Environment["COSMIC_SHORE_NET_SIM"] = po.Sim;
            if (!string.IsNullOrWhiteSpace(po?.Fault)) psi.Environment["COSMIC_SHORE_NET_FAULT"] = po.Fault;

            var logPath = Path.Combine(WorkDir, $"{index + 1}-{profile}.log");
            var log = new StreamWriter(new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            var proc = Process.Start(psi) ?? throw new InvalidOperationException("could not start " + psi.FileName);
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) => { lock (log) log.Dispose(); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return new Player { Index = index, Profile = profile, Port = port, LogPath = logPath, Process = proc };
        }

        public Player Find(string who)
        {
            if (int.TryParse(who, out int n) && n >= 1 && n <= _players.Count) return _players[n - 1];
            return _players.FirstOrDefault(p => string.Equals(p.Profile, who, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"no player '{who}' (1-{_players.Count} or {string.Join(", ", _players.Select(p => p.Profile))})");
        }

        /// <summary>One control-port command on one player: {"ok", "output", ...}.</summary>
        public static async Task<JsonObject> Command(Player p, string cmd, string arg = "")
        {
            var r = await Command(p.Port, cmd, arg);
            if (r["ok"]?.GetValue<bool>() != true && r["unreachable"] != null)
                r["output"] = p.Running ? "not answering yet (booting?)" : $"exited (code {p.Process?.ExitCode})";
            return r;
        }

        /// <summary>One control-port command on whatever player listens on <paramref name="port"/>.</summary>
        public static async Task<JsonObject> Command(int port, string cmd, string arg = "")
        {
            var body = new StringContent(JsonSerializer.Serialize(new { cmd, arg }), Encoding.UTF8, "application/json");
            try
            {
                var resp = await s_http.PostAsync($"http://127.0.0.1:{port}/", body);
                return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsObject();
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                return new JsonObject { ["ok"] = false, ["unreachable"] = true, ["output"] = "not answering (booting, or closed)" };
            }
        }

        /// <summary>
        /// One line about a player's link, from its <c>net json</c> output: role, rates, RTT per peer and
        /// the simulator. "" when the output holds no stats (networking off, or not connected yet).
        /// </summary>
        public static string NetLine(string netJsonOutput)
        {
            int brace = netJsonOutput.IndexOf('{');
            if (brace < 0) return "";
            try
            {
                var j = JsonNode.Parse(netJsonOutput[brace..])!.AsObject();
                var peers = j["peers"]?.AsArray().Select(x => $"{x?["peer"]} {(x?["rttMs"] is { } r ? r + " ms" : "-")}") ?? Enumerable.Empty<string>();
                string Kb(string key) => double.TryParse(j[key]?.ToString(), out var v) ? (v / 1024).ToString("0.0") : "?";
                var rtt = string.Join(", ", peers);
                return $"{(string?)j["role"]} · in {Kb("bytesInPerSecond")} KB/s out {Kb("bytesOutPerSecond")} KB/s · rtt {(rtt.Length > 0 ? rtt : "- (no peers)")} · sim {(string?)j["simulator"]}";
            }
            catch (JsonException) { return ""; }
        }

        /// <summary>The same command on every player (or the ones named), in parallel.</summary>
        public async Task<List<(Player player, JsonObject result)>> CommandAll(string cmd, string arg = "", IEnumerable<Player>? only = null)
        {
            var targets = (only ?? _players).ToList();
            var results = await Task.WhenAll(targets.Select(p => Command(p, cmd, arg)));
            return targets.Zip(results, (p, r) => (p, r)).ToList();
        }

        /// <summary>One line per player: scene, fps and its network monitor line.</summary>
        public async Task<string> Status()
        {
            var sb = new StringBuilder($"session folder {NetDir}\n");
            foreach (var p in _players)
            {
                var st = await Command(p, "state");
                if (st["ok"]?.GetValue<bool>() != true) { sb.AppendLine($"{p.Index + 1} {p.Profile}: {st["output"]}"); continue; }
                var net = await Command(p, "do", "net json");
                var line = NetLine(net["output"]?.ToString() ?? "");
                sb.AppendLine($"{p.Index + 1} {p.Profile}: {st["scene"]} · frame {st["frame"]}{(line.Length > 0 ? " · " + line : "")}");
            }
            return sb.ToString().TrimEnd();
        }

        public static string LogTail(Player p, int lines)
        {
            if (!File.Exists(p.LogPath)) return "";
            using var f = new FileStream(p.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var all = new StreamReader(f).ReadToEnd().Split('\n');
            return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
        }

        /// <summary>Asks every player to quit, then kills what is left.</summary>
        public async Task StopAsync()
        {
            await Task.WhenAll(_players.Where(p => p.Running).Select(p => Command(p, "quit")));
            foreach (var p in _players)
            {
                if (p.Process == null) continue;
                if (!p.Process.WaitForExit(10000)) try { p.Process.Kill(entireProcessTree: true); } catch { }
            }
        }

        public void Dispose()
        {
            foreach (var p in _players)
                if (p.Running) try { p.Process!.Kill(entireProcessTree: true); } catch { }
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
