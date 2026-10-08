using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// The engine's control port (<c>--control-port N</c> / COSMIC_SHORE_CONTROL_PORT): a local
    /// HTTP endpoint on 127.0.0.1 that lets a tool - the MCP server Claude Code talks to, or curl -
    /// drive a running player. A request is <c>POST / {"cmd": "...", "arg": "..."}</c>; the reply
    /// is <c>{"ok": bool, "output": "...", ...}</c> with everything the command printed.
    ///
    /// Commands run on the main thread between frames, exactly where a <c>--do</c> script step
    /// runs, so they see the game as the game sees itself:
    ///
    ///   state                        scene, frame, time, size, fps
    ///   do VERB ARGS                 any --do action (click X,Y / type / key / pad / inspect / eval / score ...)
    ///   wait N                       return after N more frames
    ///   screenshot [PATH]            PNG of the next presented frame (default: a temp file)
    ///   find TEXT                    objects whose name contains TEXT: path, active, components
    ///   hierarchy [ROOT[:DEPTH]]     the scene tree (roots, or the subtree under ROOT)
    ///   get OBJ COMP MEMBER          a field/property of a live component ("quote" names with spaces)
    ///   set OBJ COMP MEMBER VALUE    write one (numbers, bools, enums, "x,y,z" vectors, strings)
    ///   ui_at X,Y                    the UI graphics covering a pixel
    ///   dump_ui NAME[:DEPTH]         a UI subtree with rects, anchors and components
    ///   logs [N]                     the last N console lines (default 200)
    ///   scene NAME                   load a scene by name or build index
    ///   resize WxH | off             render (and capture) at WxH whatever the window is; off = the window's size
    ///   ui_sweep DIR[;WxH,...]       every screen of the loaded scene at each resolution: PNG + rect dump (UiSweep)
    ///   quit                         close the player
    /// </summary>
    public sealed class ControlServer : IDisposable
    {
        sealed class Pending
        {
            public string Cmd, Arg;
            public readonly TaskCompletionSource<Dictionary<string, object>> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int UntilFrame = -1;
            public StringBuilder Output;
        }

        readonly HttpListener _http = new();
        readonly ConcurrentQueue<Pending> _queue = new();
        readonly List<Pending> _waiting = new();
        readonly List<(Pending p, string path)> _shots = new();
        readonly InputScript _script;
        readonly Tee _tee;
        readonly CancellationTokenSource _stop = new();

        public int Port { get; }

        /// <summary>The player's window: closes it (quit) and reports the frame rate.</summary>
        public Action Quit;
        public Func<double> FrameMs;

        /// <summary><c>resize WxH</c>: the resolution the game renders and captures at; null = the window's.</summary>
        public (int w, int h)? VirtualSize;

        ControlServer(int port, InputScript script)
        {
            Port = port;
            _script = script;
            _tee = new Tee(Console.Out);
            Console.SetOut(_tee);
            _http.Prefixes.Add($"http://127.0.0.1:{port}/");
            _http.Start();
            Task.Run(Accept);
            Console.WriteLine($"[control] listening on http://127.0.0.1:{port}/");
        }

        /// <summary>The port from <c>--control-port</c> or COSMIC_SHORE_CONTROL_PORT; null when neither asks for one.</summary>
        public static ControlServer StartIfRequested(int port, InputScript script)
        {
            if (port <= 0 && int.TryParse(Environment.GetEnvironmentVariable("COSMIC_SHORE_CONTROL_PORT"), out var env)) port = env;
            return port > 0 ? new ControlServer(port, script) : null;
        }

        async Task Accept()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _http.GetContextAsync(); } catch { return; }
                _ = Task.Run(() => Serve(ctx));
            }
        }

        async Task Serve(HttpListenerContext ctx)
        {
            Dictionary<string, object> reply;
            try
            {
                var p = new Pending { Cmd = "state", Arg = "" };
                if (ctx.Request.HttpMethod == "POST")
                {
                    using var body = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    using var doc = JsonDocument.Parse(await body.ReadToEndAsync());
                    p.Cmd = doc.RootElement.TryGetProperty("cmd", out var c) ? c.GetString() ?? "" : "";
                    p.Arg = doc.RootElement.TryGetProperty("arg", out var a) ? a.ValueKind == JsonValueKind.String ? a.GetString() : a.ToString() : "";
                }
                _queue.Enqueue(p);
                var done = await Task.WhenAny(p.Done.Task, Task.Delay(TimeSpan.FromMinutes(15)));
                reply = done == p.Done.Task ? p.Done.Task.Result : Fail("timed out waiting for the main thread (is the game frozen?)");
            }
            catch (Exception e) { reply = Fail(e.Message); }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(reply);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            try { await ctx.Response.OutputStream.WriteAsync(bytes); ctx.Response.Close(); } catch { }
        }

        static Dictionary<string, object> Fail(string why) => new() { ["ok"] = false, ["output"] = why };

        /// <summary>Main thread, before the engine ticks: run every queued command, finish the waits that are due.</summary>
        public void BeforeTick(int frame)
        {
            if (_sweep != null)
            {
                _tee.Capture = _sweeping.Output;
                try
                {
                    if (_sweep.Step(frame)) { Finish(_sweeping, _sweep.Ok, _sweep.Result()); _sweep = null; _sweeping = null; }
                }
                catch (Exception e) { Console.WriteLine("ui_sweep: " + e.Message); Finish(_sweeping, false, null); _sweep = null; _sweeping = null; }
                finally { _tee.Capture = null; }
            }
            for (int i = _waiting.Count - 1; i >= 0; i--)
                if (frame >= _waiting[i].UntilFrame) { Finish(_waiting[i], true, null); _waiting.RemoveAt(i); }
            while (_queue.TryDequeue(out var p))
            {
                p.Output = new StringBuilder();
                _tee.Capture = p.Output;
                try { Execute(p, frame); }
                catch (Exception e) { Console.WriteLine((e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message); Finish(p, false, null); }
                finally { _tee.Capture = null; }
            }
        }

        /// <summary>Main thread, after a frame is presented: answer the screenshot requests with it.</summary>
        public void AfterPresent(Action<string> capture, int w, int h)
        {
            if (_sweep?.ShotPath is { } shot) { capture(shot); _sweep.ShotTaken(w, h); }
            foreach (var (p, path) in _shots)
            {
                try { capture(path); Finish(p, true, new() { ["path"] = path, ["width"] = w, ["height"] = h }); }
                catch (Exception e) { p.Output.AppendLine(e.Message); Finish(p, false, null); }
            }
            _shots.Clear();
        }

        public bool WantsFrame => _shots.Count > 0 || _sweep?.ShotPath != null;

        UiSweep _sweep;
        Pending _sweeping;

        void Finish(Pending p, bool ok, Dictionary<string, object> extra)
        {
            var r = new Dictionary<string, object> { ["ok"] = ok, ["output"] = p.Output?.ToString().TrimEnd() ?? "" };
            if (extra != null) foreach (var kv in extra) r[kv.Key] = kv.Value;
            p.Done.TrySetResult(r);
        }

        void Execute(Pending p, int frame)
        {
            string arg = p.Arg?.Trim() ?? "";
            switch (p.Cmd)
            {
                case "state":
                    Finish(p, true, State(frame));
                    return;
                case "do":
                    _script.Run(arg, frame);
                    break;
                case "wait":
                    p.UntilFrame = frame + Math.Max(1, int.TryParse(arg, out var n) ? n : 1);
                    _waiting.Add(p);
                    return;
                case "screenshot" when _sweep != null:
                    Console.WriteLine("a ui_sweep is running");
                    Finish(p, false, null);
                    return;
                case "screenshot":
                {
                    var path = arg.Length > 0 ? Path.GetFullPath(arg) : Path.Combine(Path.GetTempPath(), $"cosmicshore-{Port}-{frame}.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    _shots.Add((p, path));
                    return;
                }
                case "find": Find(arg); break;
                case "hierarchy": Hierarchy(arg); break;
                case "get": Member(arg, write: false); break;
                case "set": Member(arg, write: true); break;
                case "ui_at": UiDump.PrintAt(arg); break;
                case "dump_ui": UiDump.Print(arg); break;
                case "logs":
                    foreach (var line in _tee.Last(int.TryParse(arg, out var k) ? k : 200)) p.Output.AppendLine(line);
                    break;
                case "scene":
                    if (int.TryParse(arg, out var index)) SceneManager.LoadSceneAsync(index);
                    else SceneManager.LoadSceneAsync(arg);
                    Console.WriteLine($"[control] loading scene '{arg}'");
                    break;
                case "quit":
                    Quit?.Invoke();
                    break;
                case "resize":
                    VirtualSize = UiSweep.ParseSize(arg);
                    Console.WriteLine(VirtualSize is { } v ? $"[control] rendering at {v.w}x{v.h}" : "[control] rendering at the window size");
                    break;
                case "ui_sweep":
                    _sweep = new UiSweep(arg, this);
                    _sweeping = p;
                    return;
                default:
                    Console.WriteLine($"unknown command '{p.Cmd}' (state, do, wait, screenshot, find, hierarchy, get, set, ui_at, dump_ui, logs, scene, resize, ui_sweep, quit)");
                    Finish(p, false, null);
                    return;
            }
            Finish(p, true, null);
        }

        Dictionary<string, object> State(int frame) => new()
        {
            ["scene"] = SceneManager.GetActiveScene()?.name ?? "",
            ["frame"] = frame,
            ["time"] = Time.time,
            ["timeScale"] = Time.timeScale,
            ["width"] = Screen.width,
            ["height"] = Screen.height,
            ["frameMs"] = Math.Round(FrameMs?.Invoke() ?? 0, 1),
            ["behaviours"] = CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Length,
        };

        static IEnumerable<Transform> AllTransforms() =>
            CosmicShore.Engine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        static string Components(GameObject go) =>
            string.Join(", ", go.GetComponents<Component>().Where(c => c is not Transform)
                .Select(c => c.GetType().Name + (c is Behaviour b && !b.enabled ? " (off)" : "")));

        static void Find(string text)
        {
            var hits = AllTransforms().Where(t => t.name.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(100).ToList();
            Console.WriteLine($"{hits.Count} match(es){(hits.Count == 100 ? " (first 100)" : "")}");
            foreach (var t in hits)
                Console.WriteLine($"{PathOf(t)}  active={t.gameObject.activeInHierarchy}  [{Components(t.gameObject)}]");
        }

        static void Hierarchy(string spec)
        {
            int depth = 2;
            var parts = spec.Split(':');
            if (parts.Length > 1) int.TryParse(parts[1], out depth);
            var roots = parts[0].Length == 0
                ? AllTransforms().Where(t => t.parent == null).OrderBy(t => t.name).ToList()
                : AllTransforms().Where(t => t.name == parts[0] || PathOf(t) == parts[0]).ToList();
            if (roots.Count == 0) Console.WriteLine($"no object '{parts[0]}'");
            foreach (var r in roots) Walk(r, 0, depth);
        }

        static void Walk(Transform t, int level, int max)
        {
            Console.WriteLine($"{new string(' ', level * 2)}{t.name}{(t.gameObject.activeSelf ? "" : " (inactive)")}  [{Components(t.gameObject)}]{(level == max && t.childCount > 0 ? $" +{t.childCount}" : "")}");
            if (level >= max) return;
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), level + 1, max);
        }

        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>get/set OBJ COMP MEMBER [VALUE]: OBJ is a name or a path; the member may be a field or a property, inherited or not.</summary>
        static void Member(string arg, bool write)
        {
            var a = Words(arg, 4);
            if (a.Length < (write ? 4 : 3)) throw new ArgumentException(write ? "set OBJ COMP MEMBER VALUE" : "get OBJ COMP MEMBER");
            var targets = AllTransforms().Where(t => t.name == a[0] || PathOf(t) == a[0])
                .SelectMany(t => t.gameObject.GetComponents<Component>())
                .Where(c => c.GetType().Name == a[1] || c.GetType().FullName == a[1]).ToList();
            if (targets.Count == 0) throw new ArgumentException($"no {a[1]} on an object named '{a[0]}'");
            foreach (var c in targets)
            {
                FieldInfo f = null; PropertyInfo prop = null;
                for (var t = c.GetType(); t != null && f == null && prop == null; t = t.BaseType)
                {
                    f = t.GetField(a[2], Any | BindingFlags.DeclaredOnly);
                    prop = f == null ? t.GetProperty(a[2], Any | BindingFlags.DeclaredOnly) : null;
                }
                if (f == null && prop == null) throw new ArgumentException($"{c.GetType().Name} has no field or property '{a[2]}'");
                var type = f?.FieldType ?? prop.PropertyType;
                if (write)
                {
                    var value = Convert(a[3], type);
                    if (f != null) f.SetValue(c, value); else prop.SetValue(c, value);
                }
                var now = Inspector.Safe(() => f != null ? f.GetValue(c) : prop.GetValue(c));
                Console.WriteLine($"{PathOf(c.transform)} {c.GetType().Name}.{a[2]} = {Inspector.Describe(now)}");
            }
        }

        /// <summary>Space-separated words; "double quotes" keep a name with spaces together. The last word takes the rest.</summary>
        static string[] Words(string s, int max)
        {
            var words = new List<string>();
            int i = 0;
            while (i < s.Length && words.Count < max - 1)
            {
                while (i < s.Length && s[i] == ' ') i++;
                if (i >= s.Length) break;
                int end;
                if (s[i] == '"') { end = s.IndexOf('"', i + 1); if (end < 0) end = s.Length; words.Add(s[(i + 1)..end]); i = end + 1; }
                else { end = s.IndexOf(' ', i); if (end < 0) end = s.Length; words.Add(s[i..end]); i = end; }
            }
            var rest = i < s.Length ? s[i..].Trim() : "";
            if (rest.Length > 0) words.Add(rest.Length > 1 && rest[0] == '"' && rest[^1] == '"' ? rest[1..^1] : rest);
            return words.ToArray();
        }

        static object Convert(string s, Type type)
        {
            var inv = CultureInfo.InvariantCulture;
            s = s.Trim();
            if (type == typeof(string)) return s.Trim('"');
            if (type.IsEnum) return Enum.Parse(type, s, ignoreCase: true);
            if (type == typeof(bool)) return s is "1" or "true" or "True" or "on";
            float[] F() => s.Trim('(', ')').Split(',').Select(x => float.Parse(x.Trim(), inv)).ToArray();
            if (type == typeof(Vector2)) { var v = F(); return new Vector2(v[0], v[1]); }
            if (type == typeof(Vector3)) { var v = F(); return new Vector3(v[0], v[1], v[2]); }
            if (type == typeof(Color)) { var v = F(); return new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1f); }
            var u = Nullable.GetUnderlyingType(type);
            if (u != null) return s is "null" ? null : Convert(s, u);
            return System.Convert.ChangeType(s, type, inv);
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _http.Stop(); } catch { }
        }

        /// <summary>Console tee: the real stdout, a ring of recent lines, and the running command's output.</summary>
        sealed class Tee : TextWriter
        {
            readonly TextWriter _inner;
            readonly Queue<string> _ring = new();
            readonly StringBuilder _line = new();
            readonly object _lock = new();
            public StringBuilder Capture;

            public Tee(TextWriter inner) { _inner = inner; }
            public override Encoding Encoding => _inner.Encoding;

            public override void Write(char c)
            {
                _inner.Write(c);
                lock (_lock)
                {
                    Capture?.Append(c);
                    if (c == '\n') { Push(); return; }
                    if (c != '\r') _line.Append(c);
                }
            }

            public override void Write(string s)
            {
                if (s == null) return;
                _inner.Write(s);
                lock (_lock)
                {
                    Capture?.Append(s);
                    foreach (var c in s)
                    {
                        if (c == '\n') Push();
                        else if (c != '\r') _line.Append(c);
                    }
                }
            }

            public override void WriteLine(string s) { Write(s); Write('\n'); }
            public override void Flush() => _inner.Flush();

            void Push()
            {
                _ring.Enqueue(_line.ToString());
                _line.Clear();
                while (_ring.Count > 2000) _ring.Dequeue();
            }

            public List<string> Last(int n) { lock (_lock) return _ring.Skip(Math.Max(0, _ring.Count - n)).ToList(); }
        }
    }
}
