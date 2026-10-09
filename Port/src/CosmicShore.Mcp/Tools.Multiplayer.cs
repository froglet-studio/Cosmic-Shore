using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Prisma;

namespace CosmicShore.Mcp
{
    /// <summary>
    /// The multiplayer tools (docs/MULTIPLAYER.md §6): start up to four players that party up through
    /// one session folder, drive any of them, and use the network simulator, the session faults and the
    /// stats on each. The same <see cref="MultiplayerRun"/> backs the Launcher's MULTIPLAYER panel.
    /// </summary>
    static class JsonArrayExtensions
    {
        public static JsonArray AddAll(this JsonArray array, IEnumerable<JsonObject> items)
        {
            foreach (var i in items) array.Add(i);
            return array;
        }
    }

    public sealed partial class Tools
    {
        MultiplayerRun? _mp;

        static JsonObject Who() => P("string", "which player: 1-4, a profile name (PilotA...), or 'all' (default all)");

        public static IEnumerable<JsonObject> MultiplayerTools() => new[]
        {
            Tool("net_players", "Prisma's Multiplayer Play Mode: start 1-4 players of the game as separate processes that find each other through one fresh session folder (the stand-in for UGS Lobby + Relay), each with its own profile, save, control port and log; or stop them, or report each one's scene, fps, role, traffic and RTT. Windows are tiled 2x2 (under xvfb on a display-less server); headless players keep game time on the wall clock (--realtime). Party up with net_input: 'party invite PilotB' on the host's DiagnosticsHUD is run by the game's console (see Tools/Build/prisma_party_scenarios/driver.py for the console route).",
                new JsonObject
                {
                    ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("start", "stop", "status"), ["description"] = "default status" },
                    ["players"] = P("integer", "how many to start, 1-4 (default 2; a party is 4)"),
                    ["headless"] = P("boolean", "no windows (default true: fast, no screenshots)"),
                    ["profiles"] = P("string", "comma-separated profile names (default PilotA,PilotB,...); reuse names to skip the first-run prompts"),
                    ["sims"] = P("string", "comma-separated network simulator spec per player, e.g. ',4g,poor,' (empty = clean line)"),
                    ["faults"] = P("string", "comma-separated session faults armed at launch per player, e.g. ',full,,' "),
                    ["size"] = P("string", "window size per player WxH (default 960x540)"),
                    ["scene"] = P("string", "start every player in this scene"),
                    ["transport"] = P("string", "'udp' (Froglet's transport, the default) or 'tcp'; every player uses the same"),
                    ["relay"] = P("string", "'off' (default: players connect directly), 'local' (start Froglet's relay server beside them: every host allocates and every joiner joins by code, as over the internet), or a relay server's http(s) URL"),
                    ["build"] = P("boolean", "compile the player first (default true)"),
                }),
            Tool("net_input", "Run a --do action on one player or all of them (the same verbs as game_input: 'click X,Y', 'type TEXT', 'party' ..., 'net', 'netsim 4g', 'netfault full').",
                new JsonObject { ["player"] = Who(), ["action"] = P("string", "the action") }, "action"),
            Tool("net_command", "Any control-port command on one player or all: state, wait N, find TEXT, get/set, ui_at, logs N, screenshot PATH, scene NAME.",
                new JsonObject { ["player"] = Who(), ["cmd"] = P("string", "the command"), ["arg"] = P("string", "its argument") }, "cmd"),
            Tool("net_sim", "The network simulator on one player or all: latency, jitter, loss, upload bandwidth, a pulled cable. SPEC tokens apply in order: off | lan | broadband | dsl | 4g | 3g | poor | down | up | latency=MS | jitter=MS | loss=PCT | bandwidth=KBPS. Empty spec reports the current settings.",
                new JsonObject { ["player"] = Who(), ["spec"] = P("string", "e.g. '4g', 'latency=150 jitter=30', 'down', 'off'") }),
            Tool("net_fault", "Session-service faults on one player or all, to run the game's error paths: off | full[=N] | ratelimit[=N] | relayfail[=N] | down | up | slow=MS. Empty spec reports what is armed and what was raised.",
                new JsonObject { ["player"] = Who(), ["spec"] = P("string", "e.g. 'full', 'ratelimit=3', 'down', 'off'") }),
            Tool("net_stats", "The netcode's traffic on one player or all: role, bytes and messages in/out per second and in total, RTT per peer (smoothed, last, min), messages by kind, top RPCs by method. mode 'json' for the structured form, 'reset' to zero the counters, 'capture N' to record N frames of per-frame counters to a JSON file.",
                new JsonObject { ["player"] = Who(), ["mode"] = P("string", "'' (text), 'json', 'reset', or 'capture N [PATH]'") }),
            Tool("net_logs", "Recent console output of one player.",
                new JsonObject { ["player"] = P("string", "1-4 or a profile name"), ["lines"] = P("integer", "default 120"), ["grep"] = P("string", "only lines containing this") }, "player"),
            Tool("net_scenario", "Run the party layer's scenario harness (Tools/Build/prisma_party_scenarios/run.sh): five players race through invite, accept, join races, kick/leave, launch, ready gate, leaver-to-AI, spectator, host kill. ~10-15 minutes. Reports each scenario's pass/fail.",
                new JsonObject
                {
                    ["keep"] = P("boolean", "keep the worktree, logs and results.json (default false)"),
                    ["relay"] = P("boolean", "every pilot through Froglet's relay server instead of direct connections (default false)"),
                    ["sim"] = P("string", "a network simulator spec for every pilot, e.g. '4g' (default none)"),
                }),
        };

        async Task<string?> CallMultiplayer(string name, JsonObject a)
        {
            switch (name)
            {
                case "net_players":
                {
                    var action = Str(a, "action", "status");
                    if (action == "stop")
                    {
                        if (_mp == null) return "no multiplayer run";
                        await _mp.StopAsync();
                        var dir = _mp.WorkDir;
                        _mp = null;
                        return "players stopped (logs kept in " + dir + ")";
                    }
                    if (action == "status") return _mp == null ? "no multiplayer run; net_players action=start" : await _mp.Status();
                    return await StartPlayers(a);
                }
                case "net_input": return await OnPlayers(a, "do", Str(a, "action"));
                case "net_command": return await OnPlayers(a, Str(a, "cmd"), Str(a, "arg"));
                case "net_sim": return await OnPlayers(a, "do", ("netsim " + Str(a, "spec")).Trim());
                case "net_fault": return await OnPlayers(a, "do", ("netfault " + Str(a, "spec")).Trim());
                case "net_stats": return await OnPlayers(a, "do", ("net " + Str(a, "mode")).Trim());
                case "net_logs":
                {
                    var p = Mp().Find(Str(a, "player", "1"));
                    var text = MultiplayerRun.LogTail(p, Math.Max(1, Int(a, "lines", 120)) * (Str(a, "grep").Length > 0 ? 20 : 1));
                    var grep = Str(a, "grep");
                    if (grep.Length > 0)
                        text = string.Join('\n', text.Split('\n').Where(l => l.Contains(grep, StringComparison.OrdinalIgnoreCase)).TakeLast(Int(a, "lines", 120)));
                    return text.Length > 0 ? text : "(nothing)";
                }
                case "net_scenario":
                {
                    var script = Path.Combine(_repo, "Tools", "Build", "prisma_party_scenarios", "run.sh");
                    if (!File.Exists(script)) return "the scenario harness is not in this checkout: " + script;
                    var env = new Dictionary<string, string>();
                    if (Bool(a, "keep", false)) env["KEEP"] = "1";
                    if (Bool(a, "relay", false)) env["PRISMA_RELAY"] = "1";
                    if (Str(a, "sim").Length > 0) env["COSMIC_SHORE_NET_SIM"] = Str(a, "sim");
                    var r = await Run("bash", new[] { script }, _repo, TimeSpan.FromMinutes(40), env);
                    var lines = r.Output.Split('\n');
                    return $"exit {r.ExitCode} ({(r.ExitCode == 0 ? "every scenario passed" : "FAILED")})\n" + string.Join('\n', lines.TakeLast(60));
                }
                default: return null;
            }
        }

        MultiplayerRun Mp() => _mp ?? throw new ToolException("No multiplayer run. Call net_players action=start.");

        async Task<string> StartPlayers(JsonObject a)
        {
            if (_mp != null) { await _mp.StopAsync(); _mp = null; }
            var report = new StringBuilder();
            if (Bool(a, "build", true))
            {
                var b = await Build("player");
                if (!b.StartsWith("build ok")) return b;
                report.AppendLine(b.Split('\n')[0]);
            }
            string[] Split(string key) => Str(a, key).Split(',');
            var profiles = Split("profiles");
            var sims = Split("sims");
            var faults = Split("faults");
            int n = Int(a, "players", 2);
            bool headless = Bool(a, "headless", true);
            var o = new MultiplayerRun.Options
            {
                Players = n,
                Headless = headless,
                Scene = Str(a, "scene"),
                Transport = Str(a, "transport"),
                Relay = Str(a, "relay"),
                ProjectRoot = _repo,
                PlayerPath = Path.Combine(PortSrc("CosmicShore.Player"), "bin", "Debug", "net10.0", "CosmicShore.dll"),
            };
            var size = Str(a, "size", "960x540").Split('x');
            if (size.Length == 2 && int.TryParse(size[0], out var w) && int.TryParse(size[1], out var h)) { o.Width = w; o.Height = h; }
            for (int i = 0; i < n; i++)
            {
                string At(string[] arr) => i < arr.Length ? arr[i].Trim() : "";
                o.PerPlayer.Add(new MultiplayerRun.PlayerOptions { Profile = At(profiles), Sim = At(sims), Fault = At(faults) });
            }
            if (!headless && OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
                return report + "No display: start windowed players from a desktop, or pass headless=true (the default).";
            try { _mp = MultiplayerRun.Start(o); }
            catch (Exception e) when (e is ArgumentException or FileNotFoundException or InvalidOperationException) { return report + "Not started: " + e.Message; }

            // Wait until every control port answers (a first boot compiles nothing, but loads for a while).
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (DateTime.UtcNow < deadline)
            {
                var states = await _mp.CommandAll("state");
                if (states.All(s => s.result["ok"]?.GetValue<bool>() == true)) break;
                if (_mp.Players.Any(p => !p.Running)) break;
                await Task.Delay(1000);
            }
            report.AppendLine($"{n} player(s) started; work folder {_mp.WorkDir}");
            report.Append(await _mp.Status());
            return report.ToString();
        }

        async Task<string> OnPlayers(JsonObject a, string cmd, string arg)
        {
            var mp = Mp();
            var who = Str(a, "player", "all");
            var targets = who is "" or "all" ? mp.Players.ToList() : new List<MultiplayerRun.Player> { mp.Find(who) };
            var results = await mp.CommandAll(cmd, arg, targets);
            var sb = new StringBuilder();
            foreach (var (p, r) in results)
                sb.AppendLine($"── {p.Index + 1} {p.Profile}").AppendLine(Format(r));
            return sb.ToString().TrimEnd();
        }
    }
}
