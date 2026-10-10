using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio in Amoebius (/vessel-studio D33): a loopback web server that serves the studio BUILD
    /// (<see cref="StudioBuild"/>, the same bytes as claude.ai and the live mirror) and the backend the claude.ai viewer
    /// gives the artifact, behind the same <c>window.claude.use()</c> surface, so Sync, Ask, Requests and Decisions work
    /// here and the pages cannot tell the hosts apart except by what is enabled.
    ///
    /// <list type="bullet">
    /// <item><c>db</c>: <see cref="StudioStore"/>, a JSON store under the data dir (decisions / jobs / requests / data/users).</item>
    /// <item><c>user</c>: one local user for this data dir.</item>
    /// <item><c>sample</c>: <see cref="StudioAsk"/>, Amoebius's own Claude Code CLI, read-only.</item>
    /// <item><c>mcp</c> "Claude Code Remote": the session is Amoebius itself; send_message runs the job (<see cref="StudioJobs"/>).</item>
    /// </list>
    ///
    /// <para><b>Safety.</b> Binds 127.0.0.1 only, on a random port. Every URL carries a random per-launch token
    /// (<c>http://127.0.0.1:PORT/TOKEN/stoat.html</c>); anything else is 404. The Host header must be ours (no DNS
    /// rebinding), a request with another Origin or a cross-site Sec-Fetch-Site is refused, API calls must carry our
    /// Origin, no CORS headers are ever sent, and pages are served with Referrer-Policy: no-referrer so the token never
    /// reaches the font or three.js CDNs.</para>
    /// </summary>
    public sealed class StudioServer : IDisposable
    {
        public const string BridgeFile = "studio-bridge.js";
        public const string BridgeTag = "<script src=\".amoebius/bridge.js\"></script>";
        /// <summary>The session the build names (build.json "session"), so the Sync panel needs no Start session here.</summary>
        public const string LocalSession = "amoebius-local";
        public const string Ccr = "Claude Code Remote";
        public const int MaxBody = 1 << 20;

        public sealed record Options(
            string DataDir,
            string Git,
            Func<string?> Root,
            Func<string, Action<string>, CancellationToken, Task<string>>? Ask = null,
            Func<string?>? Python = null,
            Action<string>? Log = null);

        readonly Options _o;
        readonly HttpListener _http = new();
        readonly CancellationTokenSource _stop = new();
        readonly object _buildLock = new();
        StudioBuild.Info? _info;
        string? _builtKey;
        string? _refOverride;   // Refresh: origin/<branch>; null = the checkout's own branch

        public int Port { get; private set; }
        public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        public string Origin => $"http://127.0.0.1:{Port}";
        public string BaseUrl => $"{Origin}/{Token}/";
        public StudioStore Store { get; }
        public StudioJobs Jobs { get; }
        public string UserId { get; }
        public string UserName { get; }
        /// <summary>What is being served: the branch, commit and folder of the current build.</summary>
        public StudioBuild.Info? Current { get { lock (_buildLock) return _info; } }

        public StudioServer(Options o)
        {
            _o = o;
            Directory.CreateDirectory(o.DataDir);
            Store = new StudioStore(Path.Combine(o.DataDir, "db"));
            Jobs = new StudioJobs(Store, o.Root, o.Python ?? StudioJobs.FindPython, Rebuild, o.Git);
            (UserId, UserName) = LoadUser(Path.Combine(o.DataDir, "user.json"));
        }

        public StudioServer Start()
        {
            for (int attempt = 0; ; attempt++)
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                Port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                _http.Prefixes.Clear();
                _http.Prefixes.Add($"http://127.0.0.1:{Port}/");
                try { _http.Start(); break; }
                catch (HttpListenerException) when (attempt < 5) { }
            }
            _ = Task.Run(Loop);
            WriteServerFile();
            return this;
        }

        public void Dispose()
        {
            _stop.Cancel();
            try { _http.Stop(); _http.Close(); } catch { }
            try { if (File.Exists(ServerFile)) File.Delete(ServerFile); } catch (IOException) { }
        }

        /// <summary>
        /// <c>&lt;data&gt;/studio/server.json</c>: where this server listens (pid, port, token, the checkout it builds), so Unity's
        /// Vessel Studio home opens a studio on a running Amoebius instead of starting another (LaunchPrisma.OpenStudio).
        /// Under the user's own data folder, beside the launcher's settings; gone when Amoebius closes.
        /// </summary>
        public string ServerFile => Path.Combine(_o.DataDir, "server.json");

        void WriteServerFile()
        {
            try
            {
                var o = new JsonObject
                {
                    ["pid"] = Environment.ProcessId, ["port"] = Port, ["token"] = Token, ["base"] = BaseUrl,
                    ["root"] = _o.Root() is { } r ? Path.GetFullPath(r) : null,
                };
                File.WriteAllText(ServerFile, o.ToJsonString());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        /// <summary>A studio page's address: the build, with the host hash the pages read (VesselStudioTheme.host()).</summary>
        public string PageUrl(string file) => BaseUrl + Uri.EscapeDataString(file) + "#amoebius";

        /// <summary>
        /// Builds the checkout's current branch (or <paramref name="ref"/>) unless the served build already shows that commit.
        /// Returns the build being served.
        /// </summary>
        public StudioBuild.Info EnsureBuilt(string? @ref = null)
        {
            lock (_buildLock)
            {
                _refOverride = @ref;
                return BuildLocked(@ref);
            }
        }

        /// <summary>A page load: rebuild first if the branch moved (a commit, a checkout), so a reload shows it.</summary>
        StudioBuild.Info? Fresh()
        {
            lock (_buildLock)
            {
                try { return BuildLocked(_refOverride); }
                catch (Exception e) { _o.Log?.Invoke("studio rebuild skipped: " + e.Message); return _info; }
            }
        }

        StudioBuild.Info BuildLocked(string? @ref)
        {
            var root = _o.Root() ?? throw new InvalidOperationException("No checkout to build the Vessel Studio from yet.");
            {
                @ref ??= StudioBuild.CheckoutRef(_o.Git, root);
                var head = ProcessRunner.Capture(_o.Git, "-C", root, "rev-parse", @ref) ?? throw new InvalidOperationException($"{@ref} is not a commit in {root}");
                var key = root + "|" + @ref + "|" + head;
                if (_info != null && _builtKey == key && Directory.Exists(_info.OutDir)) return _info;
                var buildRoot = Path.Combine(_o.DataDir, "build");
                var dir = Path.Combine(buildRoot, DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + head[..Math.Min(10, head.Length)]);
                var info = StudioBuild.Build(_o.Git, root, @ref, dir, session: LocalSession);
                var old = _info?.OutDir;
                _info = info; _builtKey = key;
                WriteServerFile();
                _o.Log?.Invoke($"Vessel Studio built from {info.Branch} @ {info.PathSha[..7]} into {dir}");
                // keep the newest few builds (a page still open on an older one keeps working until it reloads)
                try
                {
                    foreach (var d in Directory.GetDirectories(buildRoot).OrderByDescending(d => d, StringComparer.Ordinal).Skip(3))
                        if (d != old) Directory.Delete(d, recursive: true);
                }
                catch (IOException) { }
                return info;
            }
        }

        /// <summary>Refresh: serve origin/&lt;branch&gt;; true when the studio now shows a different studio commit.</summary>
        bool Rebuild(string branch, string? pathSha)
        {
            var before = Current?.PathSha;
            var info = EnsureBuilt("origin/" + branch);
            return info.PathSha != before;
        }

        // ------------------------------------------------------------------ http

        async Task Loop()
        {
            while (!_stop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _http.GetContextAsync(); }
                catch (Exception) when (_stop.IsCancellationRequested) { return; }
                catch (HttpListenerException) { return; }
                catch (ObjectDisposedException) { return; }
                _ = Task.Run(async () =>
                {
                    try { await Handle(ctx); }
                    catch (Exception e) { _o.Log?.Invoke("studio server: " + e.Message); try { Send(ctx, 500, "text/plain", "error"); } catch { } }
                });
            }
        }

        /// <summary>Whether this request may be served at all: our Host, and no foreign Origin or cross-site fetch.</summary>
        public static bool Allowed(string? host, string? origin, string? fetchSite, int port, bool api)
        {
            if (host != $"127.0.0.1:{port}") return false;
            var ours = $"http://127.0.0.1:{port}";
            if (origin != null && origin != ours) return false;
            if (api && origin == null) return false;   // a page's fetch POST always sends Origin; anything else is not a page of ours
            if (fetchSite != null && fetchSite is not ("same-origin" or "none")) return false;
            return true;
        }

        /// <summary>The path after the token, or null when the token is missing or wrong (compared in constant time).</summary>
        public static string? AfterToken(string absPath, string token)
        {
            if (absPath.Length < token.Length + 2 || absPath[0] != '/') return null;
            var given = Encoding.ASCII.GetBytes(absPath.Substring(1, Math.Min(token.Length, absPath.Length - 1)));
            if (!CryptographicOperations.FixedTimeEquals(given, Encoding.ASCII.GetBytes(token))) return null;
            if (absPath.Length <= token.Length + 1 || absPath[token.Length + 1] != '/') return null;
            return absPath[(token.Length + 2)..];
        }

        async Task Handle(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var rest = AfterToken(req.Url!.AbsolutePath, Token);
            if (rest == null) { Send(ctx, 404, "text/plain", "not found"); return; }
            bool api = rest.StartsWith(".amoebius/", StringComparison.Ordinal) && rest != ".amoebius/bridge.js";
            if (!Allowed(req.Headers["Host"], req.Headers["Origin"], req.Headers["Sec-Fetch-Site"], Port, api))
            { Send(ctx, 403, "text/plain", "forbidden"); return; }

            if (rest == ".amoebius/bridge.js") { Send(ctx, 200, "application/javascript; charset=utf-8", Bridge()); return; }
            if (api)
            {
                if (req.HttpMethod != "POST" || !(req.ContentType ?? "").StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                { Send(ctx, 405, "text/plain", "POST application/json only"); return; }
                var body = await ReadBody(req);
                if (body == null) { Send(ctx, 413, "text/plain", "too large"); return; }
                JsonObject? q;
                try { q = JsonNode.Parse(body) as JsonObject; } catch (JsonException) { q = null; }
                if (q == null) { Send(ctx, 400, "text/plain", "bad json"); return; }
                if (rest == ".amoebius/sample") { await Sample(ctx, q); return; }
                if (rest == ".amoebius/api") { SendJson(ctx, Api(q)); return; }
                Send(ctx, 404, "text/plain", "not found"); return;
            }

            if (req.HttpMethod != "GET" && req.HttpMethod != "HEAD") { Send(ctx, 405, "text/plain", "GET only"); return; }
            if (rest.Length == 0) rest = "index.html";
            var name = Uri.UnescapeDataString(rest);
            var info = name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? Fresh() : Current;
            if (info == null) { Send(ctx, 503, "text/plain", "the studio is not built yet"); return; }
            if (!StudioBuild.SafeName(name) || !File.Exists(Path.Combine(info.OutDir, name))) { Send(ctx, 404, "text/plain", "not found"); return; }
            var bytes = File.ReadAllBytes(Path.Combine(info.OutDir, name));
            if (name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                bytes = Encoding.UTF8.GetBytes(WithBridge(Encoding.UTF8.GetString(bytes)));
            Send(ctx, 200, ContentType(name), bytes);
        }

        /// <summary>
        /// The page as built, with the backend's one script first in &lt;head&gt; (before studio-theme.js reads
        /// window.claude). The only difference from the published bytes; parity_gate.py allows exactly this.
        /// </summary>
        public static string WithBridge(string html)
        {
            var m = Regex.Match(html, @"<head(\s[^>]*)?>", RegexOptions.IgnoreCase);
            if (m.Success) return html.Insert(m.Index + m.Length, BridgeTag);
            m = Regex.Match(html, @"<!doctype[^>]*>", RegexOptions.IgnoreCase);
            return m.Success ? html.Insert(m.Index + m.Length, BridgeTag) : BridgeTag + html;
        }

        static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };

        static string? _bridge;
        static string Bridge()
        {
            if (_bridge != null) return _bridge;
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(BridgeFile)
                          ?? throw new InvalidOperationException(BridgeFile + " is not embedded");
            using var r = new StreamReader(s, Encoding.UTF8);
            return _bridge = r.ReadToEnd();
        }

        static async Task<string?> ReadBody(HttpListenerRequest req)
        {
            if (req.ContentLength64 > MaxBody) return null;
            using var ms = new MemoryStream();
            var buf = new byte[16384];
            int n;
            while ((n = await req.InputStream.ReadAsync(buf)) > 0)
            {
                ms.Write(buf, 0, n);
                if (ms.Length > MaxBody) return null;
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        static void Headers(HttpListenerResponse r)
        {
            r.Headers["Cache-Control"] = "no-store";
            r.Headers["X-Content-Type-Options"] = "nosniff";
            r.Headers["Referrer-Policy"] = "no-referrer";
        }

        static void Send(HttpListenerContext ctx, int status, string type, string text) => Send(ctx, status, type, Encoding.UTF8.GetBytes(text));

        static void Send(HttpListenerContext ctx, int status, string type, byte[] body)
        {
            var r = ctx.Response;
            r.StatusCode = status;
            r.ContentType = type;
            Headers(r);
            r.ContentLength64 = body.Length;
            if (ctx.Request.HttpMethod != "HEAD") r.OutputStream.Write(body);
            r.Close();
        }

        static void SendJson(HttpListenerContext ctx, JsonObject o) => Send(ctx, 200, "application/json; charset=utf-8", o.ToJsonString());

        static JsonObject Error(string code, string message) => new() { ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

        // ------------------------------------------------------------------ the window.claude surface

        static string? Str(JsonObject q, string k) => q[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

        /// <summary>One API call: {op, ...}. Errors come back as {error: {code, message}}, the codes the pages already read.</summary>
        public JsonObject Api(JsonObject q)
        {
            try
            {
                string op = Str(q, "op") ?? "";
                string path = Str(q, "path") ?? "", id = Str(q, "id") ?? "";
                var data = q["data"] as JsonObject;
                switch (op)
                {
                    case "use":
                        return Str(q, "name") is "db" or "user" or "sample" or "mcp"
                            ? new JsonObject { ["ok"] = true }
                            : Error("not_granted", "not available in Amoebius");
                    case "user.id": return new JsonObject { ["id"] = UserId };
                    case "user.profiles":
                    {
                        var o = new JsonObject();
                        if (q["ids"] is JsonArray ids)
                            foreach (var x in ids)
                                if (x?.ToString() == UserId) o[UserId] = new JsonObject { ["name"] = UserName };
                        return new JsonObject { ["profiles"] = o };
                    }
                    case "db.add":
                        if (data == null) return Error("invalid_argument", "no data");
                        return new JsonObject { ["id"] = Store.Add(path, data) };
                    case "db.get":
                    {
                        var d = Store.Get(path, id);
                        return new JsonObject { ["data"] = d };
                    }
                    case "db.set":
                        if (data == null) return Error("invalid_argument", "no data");
                        Store.Set(path, id, data); return new JsonObject { ["ok"] = true };
                    case "db.update":
                        if (data == null) return Error("invalid_argument", "no data");
                        Store.Update(path, id, data); return new JsonObject { ["ok"] = true };
                    case "db.delete":
                        Store.Delete(path, id); return new JsonObject { ["ok"] = true };
                    case "db.list":
                    {
                        int limit = q["limit"] is JsonValue lv && lv.TryGetValue<int>(out var l) ? l : 0;
                        bool desc = Str(q, "dir") == "desc";
                        var docs = new JsonArray();
                        foreach (var (did, d) in Store.List(path, Str(q, "orderBy"), desc, limit))
                            docs.Add(new JsonObject { ["id"] = did, ["data"] = d });
                        return new JsonObject { ["docs"] = docs, ["version"] = Store.Version(path) };
                    }
                    case "mcp.call": return Mcp(Str(q, "server"), Str(q, "tool"), q["args"] as JsonObject);
                    default: return Error("invalid_argument", "unknown op");
                }
            }
            catch (StoreException e) { return Error(e.Code, e.Message); }
        }

        static readonly Regex JobInMessage = new(@"\bjob ([A-Za-z0-9_-]{1,64})\b");

        /// <summary>The "Claude Code Remote" connector, played by Amoebius: one environment, one session (itself), and a message that names a job runs it.</summary>
        JsonObject Mcp(string? server, string? tool, JsonObject? args)
        {
            if (server != Ccr) return Error("server_not_connected", "only Sync's session connector is available in Amoebius");
            switch (tool)
            {
                case "list_environments":
                    return new JsonObject { ["result"] = new JsonObject { ["payload"] = new JsonObject { ["environments"] = new JsonArray(new JsonObject { ["environment_id"] = "env_amoebius", ["name"] = "Amoebius (this computer)" }) } } };
                case "create_session":
                    return new JsonObject { ["result"] = new JsonObject { ["payload"] = new JsonObject { ["session_id"] = LocalSession } } };
                case "send_message":
                {
                    var msg = args?["message"]?.ToString() ?? "";
                    var m = JobInMessage.Match(msg);
                    if (!m.Success) return Error("tool_error", "Amoebius runs Sync jobs; this message names none");
                    if (Store.Get("jobs", m.Groups[1].Value) == null) return Error("tool_error", "no such job");
                    Jobs.Kick(m.Groups[1].Value);
                    return new JsonObject { ["result"] = new JsonObject { ["payload"] = new JsonObject { ["ok"] = true } } };
                }
                default: return Error("tool_error", $"{tool} is not available in Amoebius");
            }
        }

        /// <summary>sample: streams NDJSON lines {"text": answer so far}, then {"done": true, "text"} or {"error": {code, message}}.</summary>
        async Task Sample(HttpListenerContext ctx, JsonObject q)
        {
            var r = ctx.Response;
            r.StatusCode = 200;
            r.ContentType = "application/x-ndjson; charset=utf-8";
            Headers(r);
            r.SendChunked = true;
            var gate = new SemaphoreSlim(1, 1);
            async Task Line(JsonObject o)
            {
                var b = Encoding.UTF8.GetBytes(o.ToJsonString() + "\n");
                await gate.WaitAsync();
                try { await r.OutputStream.WriteAsync(b); await r.OutputStream.FlushAsync(); } finally { gate.Release(); }
            }
            try
            {
                if (_o.Ask == null) throw new StudioAsk.AskException("not_granted", "Asking is not available in this Amoebius");
                var prompt = Str(q, "prompt") ?? "";
                var answer = await _o.Ask(prompt, t => { try { Line(new JsonObject { ["text"] = t }).Wait(); } catch { } }, _stop.Token);
                await Line(new JsonObject { ["done"] = true, ["text"] = answer });
            }
            catch (StudioAsk.AskException e) { await Line(Error(e.Code, e.Message)); }
            catch (Exception e) { await Line(Error("error", e.Message)); }
            finally { try { r.Close(); } catch { } }
        }

        static (string id, string name) LoadUser(string file)
        {
            try
            {
                if (File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject o && Str(o, "id") is { } id && StudioStore.ValidId(id))
                    return (id, Str(o, "name") ?? Environment.UserName);
            }
            catch (JsonException) { }
            var fresh = ("amoebius-" + StudioStore.NewId()[..12], string.IsNullOrWhiteSpace(Environment.UserName) ? "You" : Environment.UserName);
            File.WriteAllText(file, new JsonObject { ["id"] = fresh.Item1, ["name"] = fresh.Item2 }.ToJsonString());
            return fresh;
        }
    }
}
