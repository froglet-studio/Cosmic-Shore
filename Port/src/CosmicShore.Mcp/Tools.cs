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
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Mcp
{
    /// <summary>The engine's verbs as MCP tools. One player at a time, owned by this server (or attached by port).</summary>
    public sealed class Tools : IDisposable
    {
        public const string Instructions =
            "Froglet Engine (the Cosmic Shore .NET port). Read Port/CLAUDE.md first. Loop: edit code -> engine_build -> " +
            "game_start -> game_state / game_screenshot / game_input / game_get ... -> game_stop. The port must not change " +
            "Assets/, Packages/ or ProjectSettings/ (unity_isolation_check). Coordinates are screenshot pixels, top-left origin.";

        readonly string _repo;
        readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(16) };
        Process? _game;
        string? _gameLog;
        int _port;

        public Tools(string repo) { _repo = repo; }

        string PortSrc(string project) => Path.Combine(_repo, "Port", "src", project);

        // ---- the catalogue ---------------------------------------------------------------

        static JsonObject Tool(string name, string description, JsonObject? props = null, params string[] required)
        {
            var schema = new JsonObject { ["type"] = "object", ["properties"] = props ?? new JsonObject() };
            if (required.Length > 0) schema["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray());
            return new JsonObject { ["name"] = name, ["description"] = description, ["inputSchema"] = schema };
        }

        static JsonObject P(string type, string description) => new() { ["type"] = type, ["description"] = description };

        public static JsonArray List() => new()
        {
            Tool("engine_build", "Compile part of the engine and report its errors. Run after every C# change (Port/src or Assets/_Scripts, which the player compiles live).",
                new JsonObject { ["target"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("player", "launcher", "build-tool", "mcp", "all"), ["description"] = "default player (the game: engine + render + the live Assets/_Scripts compile)" } }),
            Tool("engine_test", "Run the port's unit tests (dotnet test).",
                new JsonObject
                {
                    ["filter"] = P("string", "dotnet test --filter expression, e.g. FullyQualifiedName~SceneModel"),
                    ["suite"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("engine", "ported"), ["description"] = "engine = Port/tests/CosmicShore.Tests (default); ported = the game's own tests" },
                }),
            Tool("unity_isolation_check", "Fails if the branch changes anything outside Port/ that Unity would see. Run before committing.",
                new JsonObject { ["base"] = P("string", "branch to diff against (default origin/bleeding-edge)") }),
            Tool("game_start", "Build (unless build=false) and start the player with its control port, then wait until it answers. On a Linux server without a display it runs under xvfb-run. Stops a previous player first.",
                new JsonObject
                {
                    ["scene"] = P("string", "start in this scene instead of build scene 0 (Bootstrap)"),
                    ["size"] = P("string", "window size WxH (default 1280x720)"),
                    ["headless"] = P("boolean", "no window: fast ticks, no screenshots"),
                    ["online"] = P("boolean", "real networking (default false: one offline process)"),
                    ["audio"] = P("boolean", "FMOD audio (default false)"),
                    ["profile"] = P("string", "save profile name (COSMIC_SHORE_PROFILE); a new name is a first-time user"),
                    ["build"] = P("boolean", "compile first (default true)"),
                    ["args"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "extra player arguments (--msaa 8, --verbose, --do 300:click 10,10 ...)" },
                }),
            Tool("game_attach", "Use a player someone else started with --control-port N.",
                new JsonObject { ["port"] = P("integer", "its control port") }, "port"),
            Tool("game_stop", "Close the player (asks it to quit, then kills it)."),
            Tool("game_state", "Active scene, frame, game time, window size, frame time, live behaviour count."),
            Tool("game_screenshot", "PNG of the next presented frame, returned as an image.",
                new JsonObject { ["path"] = P("string", "also keep it at this path") }),
            Tool("game_input", "Run one input/diagnostic action now, the same verbs as --do: 'click X,Y', 'move X,Y', 'type TEXT', 'key Enter', 'hold W 30', 'pad buttonSouth', 'inspect OBJ COMP', 'eval Type.Static.member', 'score', 'vessels', 'timescale 4'.",
                new JsonObject { ["action"] = P("string", "the action") }, "action"),
            Tool("game_wait", "Let the game run N frames, then return (input needs a few frames to land; scene loads need more).",
                new JsonObject { ["frames"] = P("integer", "frames to wait (default 60)") }),
            Tool("game_find", "Every object whose name contains the text: hierarchy path, active, components.",
                new JsonObject { ["text"] = P("string", "part of a name") }, "text"),
            Tool("game_hierarchy", "The live scene tree: the roots, or the subtree under one object.",
                new JsonObject { ["root"] = P("string", "object name or path (default: all roots)"), ["depth"] = P("integer", "levels (default 2)") }),
            Tool("game_get", "Read a field or property of a live component.",
                new JsonObject { ["object"] = P("string", "object name or hierarchy path"), ["component"] = P("string", "component type name"), ["member"] = P("string", "field or property") },
                "object", "component", "member"),
            Tool("game_set", "Write a field or property of a live component (numbers, bools, enum names, 'x,y,z' vectors, 'r,g,b,a' colours, strings).",
                new JsonObject { ["object"] = P("string", "object name or hierarchy path"), ["component"] = P("string", "component type name"), ["member"] = P("string", "field or property"), ["value"] = P("string", "new value") },
                "object", "component", "member", "value"),
            Tool("game_ui_at", "Which UI graphics cover a pixel (path, sprite, colour, alpha, rect) - 'what is that on screen?'.",
                new JsonObject { ["x"] = P("number", "screenshot x"), ["y"] = P("number", "screenshot y (top-left origin)") }, "x", "y"),
            Tool("game_dump_ui", "A UI subtree with world rects, anchors, pivots, sizes and components.",
                new JsonObject { ["name"] = P("string", "object name"), ["depth"] = P("integer", "levels (default 4)") }, "name"),
            Tool("game_logs", "The player's recent console output.",
                new JsonObject { ["lines"] = P("integer", "how many (default 200)"), ["grep"] = P("string", "only lines containing this") }),
            Tool("game_load_scene", "Load a scene by name or build index.",
                new JsonObject { ["scene"] = P("string", "scene name or build index") }, "scene"),
        };

        public static JsonObject Text(string s) => new() { ["type"] = "text", ["text"] = s };

        static string Str(JsonObject a, string k, string def = "") => a[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : a[k]?.ToString() ?? def;
        static int Int(JsonObject a, string k, int def) => a[k] is JsonValue v && (v.TryGetValue<int>(out var i) || (v.TryGetValue<string>(out var s) && int.TryParse(s, out i))) ? i : def;
        static bool Bool(JsonObject a, string k, bool def) => a[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;

        static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

        public async Task<JsonArray> Call(string name, JsonObject a)
        {
            switch (name)
            {
                case "engine_build": return new JsonArray(Text(await Build(Str(a, "target", "player"))));
                case "engine_test": return new JsonArray(Text(await Test(Str(a, "suite", "engine"), Str(a, "filter"))));
                case "unity_isolation_check":
                {
                    var args = new List<string> { Path.Combine(_repo, "Port", "tools", "check_unity_isolation.py") };
                    if (Str(a, "base").Length > 0) { args.Add("--base"); args.Add(Str(a, "base")); }
                    var r = await Run(OperatingSystem.IsWindows() ? "python" : "python3", args, _repo, TimeSpan.FromMinutes(2));
                    return new JsonArray(Text(r.Output.Trim()));
                }
                case "game_start": return new JsonArray(Text(await Start(a)));
                case "game_attach":
                {
                    _port = Int(a, "port", 0);
                    return new JsonArray(Text(Format(await Command("state", ""))));
                }
                case "game_stop": return new JsonArray(Text(await Stop()));
                case "game_state": return new JsonArray(Text(Format(await Command("state", ""))));
                case "game_screenshot":
                {
                    var path = Str(a, "path");
                    var keep = path.Length > 0;
                    if (!keep) path = Path.Combine(Path.GetTempPath(), "froglet-mcp", $"shot-{DateTime.Now:HHmmss-fff}.png");
                    var r = await Command("screenshot", path);
                    if (!Ok(r)) return new JsonArray(Text(Format(r)));
                    var png = await File.ReadAllBytesAsync(path);
                    if (!keep) File.Delete(path);
                    return new JsonArray(
                        new JsonObject { ["type"] = "image", ["data"] = Convert.ToBase64String(png), ["mimeType"] = "image/png" },
                        Text($"{r["width"]}x{r["height"]}" + (keep ? " saved to " + path : "")));
                }
                case "game_input": return new JsonArray(Text(Format(await Command("do", Str(a, "action")))));
                case "game_wait": return new JsonArray(Text(Format(await Command("wait", Int(a, "frames", 60).ToString()))));
                case "game_find": return new JsonArray(Text(Format(await Command("find", Str(a, "text")))));
                case "game_hierarchy": return new JsonArray(Text(Format(await Command("hierarchy", Str(a, "root") + ":" + Int(a, "depth", 2)))));
                case "game_get":
                    return new JsonArray(Text(Format(await Command("get", $"{Quote(Str(a, "object"))} {Str(a, "component")} {Str(a, "member")}"))));
                case "game_set":
                    return new JsonArray(Text(Format(await Command("set", $"{Quote(Str(a, "object"))} {Str(a, "component")} {Str(a, "member")} {Str(a, "value")}"))));
                case "game_ui_at": return new JsonArray(Text(Format(await Command("ui_at", $"{Str(a, "x")},{Str(a, "y")}"))));
                case "game_dump_ui": return new JsonArray(Text(Format(await Command("dump_ui", Str(a, "name") + ":" + Int(a, "depth", 4)))));
                case "game_logs":
                {
                    var r = await Command("logs", Int(a, "lines", 200).ToString());
                    var grep = Str(a, "grep");
                    var text = r["output"]?.ToString() ?? "";
                    if (grep.Length > 0) text = string.Join('\n', text.Split('\n').Where(l => l.Contains(grep, StringComparison.OrdinalIgnoreCase)));
                    return new JsonArray(Text(text.Length > 0 ? text : "(nothing)"));
                }
                case "game_load_scene": return new JsonArray(Text(Format(await Command("scene", Str(a, "scene")))));
                default: throw new ToolException("unknown tool " + name);
            }
        }

        // ---- the player --------------------------------------------------------------------

        static bool Ok(JsonObject r) => r["ok"]?.GetValue<bool>() == true;

        static string Format(JsonObject r)
        {
            var sb = new StringBuilder();
            if (!Ok(r)) sb.Append("FAILED: ");
            var output = r["output"]?.ToString() ?? "";
            var rest = r.Where(kv => kv.Key is not ("ok" or "output")).Select(kv => $"{kv.Key}={kv.Value}").ToList();
            if (rest.Count > 0) sb.AppendLine(string.Join("  ", rest));
            sb.Append(output.Length > 0 ? output : rest.Count > 0 ? "" : "done");
            var s = sb.ToString().Trim();
            return s.Length > 60000 ? s[..60000] + "\n... (truncated)" : s;
        }

        async Task<JsonObject> Command(string cmd, string arg)
        {
            if (_port == 0) throw new ToolException("No player is running. Call game_start (or game_attach).");
            var body = new StringContent(JsonSerializer.Serialize(new { cmd, arg }), Encoding.UTF8, "application/json");
            HttpResponseMessage resp;
            try { resp = await _http.PostAsync($"http://127.0.0.1:{_port}/", body); }
            catch (HttpRequestException)
            {
                throw new ToolException("The player is not answering" + (_game is { HasExited: true } ? $" - it exited with code {_game.ExitCode}.\n" + LogTail(60) : "."));
            }
            return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!.AsObject();
        }

        string LogTail(int lines)
        {
            if (_gameLog == null || !File.Exists(_gameLog)) return "";
            using var f = new FileStream(_gameLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var all = new StreamReader(f).ReadToEnd().Split('\n');
            return string.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return p;
        }

        async Task<string> Start(JsonObject a)
        {
            await Stop();
            var report = new StringBuilder();
            if (Bool(a, "build", true))
            {
                var b = await Build("player");
                if (!b.StartsWith("build ok")) return b;
                report.AppendLine(b.Split('\n')[0]);
            }
            var exe = Path.Combine(PortSrc("CosmicShore.Player"), "bin", "Debug", "net10.0", OperatingSystem.IsWindows() ? "CosmicShore.exe" : "CosmicShore");
            if (!File.Exists(exe)) throw new ToolException("The player is not built: " + exe);

            _port = FreePort();
            string size = Str(a, "size", "1280x720");
            bool headless = Bool(a, "headless", false);
            var args = new List<string> { "--size", size, "--control-port", _port.ToString() };
            if (headless) args.Add("--headless");
            if (Str(a, "scene").Length > 0) { args.Add("--scene"); args.Add(Str(a, "scene")); }
            if (a["args"] is JsonArray extra) args.AddRange(extra.Select(x => x?.ToString() ?? "").Where(x => x.Length > 0));

            string file = exe;
            if (!headless && OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            {
                // A server with no display: a virtual one, a little larger than the window.
                var wh = size.Split('x');
                int w = int.TryParse(wh[0], out var pw) ? pw + 120 : 1400, h = wh.Length > 1 && int.TryParse(wh[1], out var ph) ? ph + 80 : 800;
                args.InsertRange(0, new[] { "-a", "-s", $"-screen 0 {w}x{h}x24", exe });
                file = "xvfb-run";
                report.AppendLine("no display: running under xvfb-run (software GL, expect a few frames per second)");
            }

            var psi = new ProcessStartInfo(file)
            {
                WorkingDirectory = _repo,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            foreach (var x in args) psi.ArgumentList.Add(x);
            if (!Bool(a, "online", false)) psi.Environment["COSMIC_SHORE_NET"] = "off";
            if (!Bool(a, "audio", false)) psi.Environment["COSMIC_SHORE_AUDIO"] = "off";
            if (Str(a, "profile").Length > 0) psi.Environment["COSMIC_SHORE_PROFILE"] = Str(a, "profile");

            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "froglet-mcp"));
            _gameLog = Path.Combine(Path.GetTempPath(), "froglet-mcp", $"player-{_port}.log");
            var log = new StreamWriter(new FileStream(_gameLog, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            _game = Process.Start(psi) ?? throw new ToolException("could not start " + file);
            _game.OutputDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
            _game.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (log) log.WriteLine(e.Data); };
            _game.BeginOutputReadLine();
            _game.BeginErrorReadLine();
            _game.Exited += (_, _) => { lock (log) log.Dispose(); };
            _game.EnableRaisingEvents = true;

            var deadline = DateTime.UtcNow.AddMinutes(4);
            while (DateTime.UtcNow < deadline)
            {
                if (_game.HasExited)
                {
                    _port = 0;
                    return report + $"The player exited during boot (code {_game.ExitCode}):\n" + LogTail(80);
                }
                try
                {
                    var r = await Command("state", "");
                    if (Ok(r)) return report + "player running (log: " + _gameLog + ")\n" + Format(r);
                }
                catch (ToolException) { }
                await Task.Delay(1000);
            }
            return report + "The player started but its control port never answered:\n" + LogTail(60);
        }

        async Task<string> Stop()
        {
            if (_game == null) { if (_port != 0) { try { await Command("quit", ""); } catch { } _port = 0; return "asked the attached player to quit"; } return "no player running"; }
            var g = _game;
            _game = null;
            if (!g.HasExited)
            {
                try { await Command("quit", ""); } catch { }
                if (!g.WaitForExit(15000)) try { g.Kill(entireProcessTree: true); } catch { }
            }
            _port = 0;
            return "player stopped";
        }

        // ---- build & test ----------------------------------------------------------------------

        async Task<string> Build(string target)
        {
            string path = target switch
            {
                "launcher" => PortSrc("CosmicShore.Launcher"),
                "build-tool" => PortSrc("CosmicShore.Build"),
                "mcp" => PortSrc("CosmicShore.Mcp"),
                "all" => Path.Combine(_repo, "Port", "CosmicShore.slnx"),
                _ => PortSrc("CosmicShore.Player"),
            };
            var sw = Stopwatch.StartNew();
            var r = await Run("dotnet", new[] { "build", path, "-nologo", "-v", "q", "-clp:NoSummary" }, _repo, TimeSpan.FromMinutes(25));
            var lines = r.Output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var errors = lines.Where(l => l.Contains(": error ")).Distinct().ToList();
            int warnings = lines.Where(l => l.Contains(": warning ")).Distinct().Count();
            if (r.ExitCode == 0) return $"build ok: {target} in {sw.Elapsed.TotalSeconds:F0} s ({warnings} warning(s))";
            var shown = errors.Count > 0 ? errors : lines.TakeLast(40).ToList();
            return $"build FAILED: {target}, {errors.Count} error(s)\n" + string.Join('\n', shown.Take(60).Select(e => e.Replace(_repo + Path.DirectorySeparatorChar, "")));
        }

        async Task<string> Test(string suite, string filter)
        {
            var proj = Path.Combine(_repo, "Port", "tests", suite == "ported" ? "CosmicShore.Tests.Ported" : "CosmicShore.Tests");
            var args = new List<string> { "test", proj, "-nologo", "-v", "q" };
            if (filter.Length > 0) { args.Add("--filter"); args.Add(filter); }
            var r = await Run("dotnet", args, _repo, TimeSpan.FromMinutes(30));
            var lines = r.Output.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
            var failed = lines.Where(l => l.Contains("Failed ") || l.Contains("error") || l.Contains("Assert")).Take(80);
            var summary = lines.Where(l => l.Contains("Passed!") || l.Contains("Failed!") || l.Contains("Total tests") || l.Contains("Passed:") || l.Contains("Failed:")).ToList();
            return (r.ExitCode == 0 ? "tests passed\n" : "tests FAILED\n") + string.Join('\n', summary.Count > 0 ? summary : lines.TakeLast(20))
                   + (r.ExitCode == 0 ? "" : "\n" + string.Join('\n', failed));
        }

        sealed record RunResult(int ExitCode, string Output);

        static async Task<RunResult> Run(string file, IEnumerable<string> args, string cwd, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo(file) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.Environment["DOTNET_NOLOGO"] = "1";
            psi.Environment["MSBUILDTERMINALLOGGER"] = "off";
            using var p = Process.Start(psi) ?? throw new ToolException("could not start " + file);
            p.StandardInput.Close();
            var output = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using var cts = new CancellationTokenSource(timeout);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException) { try { p.Kill(true); } catch { } return new RunResult(-1, output + "\n(timed out)"); }
            lock (output) return new RunResult(p.ExitCode, output.ToString());
        }

        public void Dispose()
        {
            if (_game is { HasExited: false }) try { _game.Kill(entireProcessTree: true); } catch { }
        }
    }
}
