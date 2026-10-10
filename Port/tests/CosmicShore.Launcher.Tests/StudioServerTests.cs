using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Launcher;
using Xunit;

namespace CosmicShore.Launcher.Tests
{
    /// <summary>A throwaway git repo with a minimal Vessel Studio, for the build and the server.</summary>
    sealed class StudioFixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "studio-fx-" + Guid.NewGuid().ToString("N"));
        public readonly string Data;
        public StudioFixture()
        {
            Data = Path.Combine(Root, "_data");
            var dir = Path.Combine(Root, "repo", "Docs", "Studios", "VesselStudio");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "studios.json"), "{ \"studios\": [ { \"id\": \"stoat\", \"name\": \"Stoat\", \"file\": \"stoat.html\" } ], \"shared\": [ \"studio-theme.js\" ] }");
            File.WriteAllText(Path.Combine(dir, "index.html"), "<!doctype html><html><head><title>hub</title></head><body>hub<script src=\"sync.js\"></script></body></html>");
            File.WriteAllText(Path.Combine(dir, "stoat.html"), "<!doctype html>\n<html>\n<HEAD lang=\"en\">\n<title>stoat</title>\n</HEAD>\n<body>stoat \u00b7 page</body>\n</html>\n");
            File.WriteAllText(Path.Combine(dir, "sync.js"), "/* sync */");
            File.WriteAllText(Path.Combine(dir, "studio-theme.js"), "/* theme */");
            Git("init", "-q", "-b", "studio-test");
            Git("-c", "user.email=t@t", "-c", "user.name=t", "add", "-A");
            Git("-c", "user.email=t@t", "-c", "user.name=t", "commit", "-q", "-m", "studio: first \u00b7 \"quoted\"");
        }
        public string Repo => Path.Combine(Root, "repo");
        public void Git(params string[] a)
        {
            var psi = new ProcessStartInfo("git") { WorkingDirectory = Repo, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var x in a) psi.ArgumentList.Add(x);
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception("git " + string.Join(' ', a) + ": " + p.StandardError.ReadToEnd());
        }
        public void Dispose() { try { Directory.Delete(Root, true); } catch { } }
    }

    public class StudioBuildTests
    {
        [Fact]
        public void Build_WritesThePagesTheCatalogAndBuildJson_InjectingTheTagOnlyWhereMissing()
        {
            using var fx = new StudioFixture();
            var outDir = Path.Combine(fx.Root, "out");
            var info = StudioBuild.Build("git", fx.Repo, StudioBuild.CheckoutRef("git", fx.Repo), outDir, session: "s1");
            Assert.Equal("studio-test", info.Branch);
            Assert.Equal(new[] { "index.html", "stoat.html" }, info.Pages);
            var names = Directory.GetFiles(outDir).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "build.json", "index.html", "stoat.html", "studio-theme.js", "studios.json", "sync.js" }, names);
            Assert.Equal(1, CountOf(File.ReadAllText(Path.Combine(outDir, "index.html")), "src=\"sync.js\""));   // already had it
            Assert.Contains("stoat \u00b7 page" + StudioBuild.Tag + "</body>", File.ReadAllText(Path.Combine(outDir, "stoat.html")));   // injected
            var b = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "build.json")))!;
            Assert.Equal("studio-test", b["branch"]!.ToString());
            Assert.Equal("s1", b["session"]!.ToString());
            Assert.Equal(40, b["pathSha"]!.ToString().Length);
            Assert.Contains("\\u00b7 \\\"quoted\\\"", File.ReadAllText(Path.Combine(outDir, "build.json")));   // Python's ensure_ascii
        }

        [Fact]
        public void Build_RefusesACatalogThatNamesAFileOutsideTheFolder()
        {
            using var fx = new StudioFixture();
            File.WriteAllText(Path.Combine(fx.Repo, StudioBuild.Dir, "studios.json"), "{ \"studios\": [ { \"file\": \"../../x.html\" } ] }");
            fx.Git("-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qam", "bad");
            Assert.Throws<InvalidOperationException>(() => StudioBuild.Build("git", fx.Repo, "HEAD", Path.Combine(fx.Root, "out")));
        }

        [Fact]
        public void PyString_MatchesPythonsJsonDumps()
        {
            Assert.Equal("\"a\\\"b\\\\c\\n\\u00b7\\u007f\\u001f'<>&\"", StudioBuild.PyString("a\"b\\c\n\u00b7\u007f\u001f'<>&"));
        }

        /// <summary>
        /// The C# build is the Python build: same ref of THIS repo, both ways, every file byte for byte (build.json by
        /// field, without the host's own artifact/session). Runs where the repo and python3 are present (CI, a checkout).
        /// </summary>
        [Fact]
        public void Build_IsByteIdenticalToBuildArtifactPy_OnThisRepo()
        {
            var root = RepoRoot();
            var py = StudioJobs.FindPython();
            if (root == null || py == null || py.Contains('\u0001')) return;   // not in a checkout with python3: nothing to compare
            var tmp = Path.Combine(Path.GetTempPath(), "vs-parity-" + Guid.NewGuid().ToString("N"));
            try
            {
                string a = Path.Combine(tmp, "py"), b = Path.Combine(tmp, "cs");
                var psi = new ProcessStartInfo(py) { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var x in new[] { ".claude/skills/vessel-studio/build_artifact.py", "--ref", "HEAD", "--out", a }) psi.ArgumentList.Add(x);
                using (var p = Process.Start(psi)!) { p.StandardOutput.ReadToEnd(); p.WaitForExit(); Assert.True(p.ExitCode == 0, p.StandardError.ReadToEnd()); }
                StudioBuild.Build("git", root, "HEAD", b);
                var fa = Directory.GetFiles(a).Select(f => Path.GetFileName(f)).OrderBy(x => x).ToArray();
                Assert.Equal(fa, Directory.GetFiles(b).Select(Path.GetFileName).OrderBy(x => x).ToArray());
                foreach (var f in fa.Select(x => x!))
                {
                    if (f == "build.json")
                    {
                        JsonObject ja = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(a, f)))!, jb = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(b, f)))!;
                        foreach (var k in new[] { "repo", "branch", "sha", "pathSha", "subject", "committedAt" }) Assert.Equal(ja[k]?.ToString(), jb[k]?.ToString());
                        continue;
                    }
                    Assert.True(File.ReadAllBytes(Path.Combine(a, f)).SequenceEqual(File.ReadAllBytes(Path.Combine(b, f))), f + " differs from build_artifact.py's");
                }
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        internal static string? RepoRoot()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, ".claude", "skills", "vessel-studio", "build_artifact.py")) &&
                    File.Exists(Path.Combine(d.FullName, StudioBuild.Dir, "studios.json"))) return d.FullName;
            return null;
        }

        static int CountOf(string s, string sub) { int n = 0, i = 0; while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; } return n; }
    }

    public class StudioStoreTests
    {
        static StudioStore New(out string dir) { dir = Path.Combine(Path.GetTempPath(), "store-" + Guid.NewGuid().ToString("N")); return new StudioStore(dir); }

        [Fact]
        public void Add_Get_Update_Delete_AndItSurvivesARestart()
        {
            var s = New(out var dir);
            try
            {
                var id = s.Add("decisions", new JsonObject { ["text"] = "go", ["at"] = "2026-10-10T01:00:00Z" });
                Assert.Equal(20, id.Length);
                s.Update("decisions", id, new JsonObject { ["kind"] = "decision" });
                var again = new StudioStore(dir).Get("decisions", id)!;
                Assert.Equal("go", again["text"]!.ToString());
                Assert.Equal("decision", again["kind"]!.ToString());
                var e = Assert.Throws<StoreException>(() => s.Update("decisions", "nope", new JsonObject { ["x"] = 1 }));
                Assert.Equal("not_found", e.Code);
                Assert.True(s.Delete("decisions", id));
                Assert.Null(s.Get("decisions", id));
                s.Set("data/users/amoebius-x", "sync", new JsonObject { ["session"] = "s" });
                Assert.Equal("s", new StudioStore(dir).Get("data/users/amoebius-x", "sync")!["session"]!.ToString());
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void List_OrdersLikeFirestore_LeavingOutDocsWithoutTheField()
        {
            var s = New(out var dir);
            try
            {
                s.Add("decisions", new JsonObject { ["text"] = "b", ["at"] = "2026-10-10T02:00:00Z" });
                s.Add("decisions", new JsonObject { ["text"] = "a", ["at"] = "2026-10-10T01:00:00Z" });
                s.Add("decisions", new JsonObject { ["text"] = "stoat", ["createdAt"] = 5 });   // the Stoat's log: no "at"
                s.Add("decisions", new JsonObject { ["text"] = "c", ["at"] = "2026-10-10T03:00:00Z" });
                Assert.Equal(new[] { "c", "b" }, s.List("decisions", "at", true, 2).Select(d => d.Data["text"]!.ToString()));
                Assert.Equal(new[] { "a", "b", "c" }, s.List("decisions", "at", false, 0).Select(d => d.Data["text"]!.ToString()));
                Assert.Equal(new[] { "stoat" }, s.List("decisions", "createdAt", true, 300).Select(d => d.Data["text"]!.ToString()));
                Assert.Equal(4, s.List("decisions", null, false, 0).Count);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Theory]
        [InlineData("../x")] [InlineData("a/../b")] [InlineData("/abs")] [InlineData("a//b")] [InlineData(".hidden")] [InlineData("a/.x")] [InlineData("")] [InlineData("a b")]
        public void BadCollectionPathsAreRefused(string path)
        {
            var s = New(out var dir);
            try { Assert.Equal("invalid_argument", Assert.Throws<StoreException>(() => s.Add(path, new JsonObject())).Code); }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public void BadIdsAndHugeDocsAreRefused()
        {
            var s = New(out var dir);
            try
            {
                Assert.Throws<StoreException>(() => s.Set("jobs", "../x", new JsonObject()));
                Assert.Throws<StoreException>(() => s.Add("jobs", new JsonObject { ["x"] = new string('a', StudioStore.MaxDocBytes + 1) }));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }

    public class StudioJobsTests
    {
        static JsonObject A(params (string k, string v)[] kv) { var o = new JsonObject(); foreach (var (k, v) in kv) o[k] = v; return o; }

        [Theory]
        [InlineData("bleeding-edge")] [InlineData("Ys-bleeding-edge")] [InlineData("main")] [InlineData("master")]
        public void NeverMergesIntoOrDeletesASharedBaseBranch(string b)
        {
            Assert.StartsWith("refused", StudioJobs.Refuse("merge", A(("from", "feature/x"), ("to", b)), null));
            Assert.StartsWith("refused", StudioJobs.Refuse("delete", A(("branch", b)), null));
            Assert.Null(StudioJobs.Refuse("compare", A(("from", "feature/x"), ("to", b)), null));   // comparing is read-only
            Assert.Null(StudioJobs.Refuse("merge", A(("from", b), ("to", "feature/x")), null));      // merging a base INTO a branch is fine
        }

        [Fact]
        public void NeverDeletesTheCheckoutsOwnBranch_AndAlwaysKeepsIt()
        {
            Assert.StartsWith("refused", StudioJobs.Refuse("delete", A(("branch", "my/work")), "my/work"));
            var args = StudioJobs.Args("delete", A(("branch", "old/x")), "my/work");
            Assert.Equal(new[] { "delete", "--branch", "old/x", "--yes", "--keep", "Ys-bleeding-edge", "--keep", "my/work" }, args);
        }

        [Theory]
        [InlineData("-rf")] [InlineData("a..b")] [InlineData("a b")] [InlineData("x;rm -rf ~")] [InlineData("a//b")] [InlineData("x.lock")]
        [InlineData("a/")] [InlineData("@{-1}")] [InlineData("$(id)")] [InlineData("")] [InlineData("--force")]
        public void BranchNamesFollowTheRegex(string bad)
        {
            Assert.False(StudioJobs.IsBranch(bad));
            Assert.StartsWith("refused", StudioJobs.Refuse("compare", A(("from", bad), ("to", "main")), null));
            Assert.StartsWith("refused", StudioJobs.Refuse("refresh", A(("branch", bad)), null));
        }

        [Fact]
        public void TheArgsAreSyncJobPys_WithNoForceAnywhere()
        {
            Assert.True(StudioJobs.IsBranch("cece/magical-carson-9bdq8z"));
            Assert.Equal(new[] { "compare", "--from", "a", "--to", "b" }, StudioJobs.Args("compare", A(("from", "a"), ("to", "b")), null));
            Assert.Equal(new[] { "merge", "--from", "a", "--to", "b", "--yes" }, StudioJobs.Args("merge", A(("from", "a"), ("to", "b")), null));
            Assert.Equal(new[] { "status", "--branch", "a", "--shown", "abc1234" }, StudioJobs.Args("refresh", A(("branch", "a"), ("shown", "abc1234")), null));
            foreach (var k in StudioJobs.Kinds)
                Assert.DoesNotContain(StudioJobs.Args(k, A(("branch", "a"), ("from", "a"), ("to", "b")), null), x => x.Contains("force") || x == "-f");
            Assert.StartsWith("refused", StudioJobs.Refuse("push", A(), null));
            Assert.StartsWith("refused", StudioJobs.Refuse("refresh", A(("branch", "a"), ("shown", "; rm")), null));
        }

        [Fact]
        public async Task ARefusedJobFailsWithoutRunningAnything()
        {
            var dir = Path.Combine(Path.GetTempPath(), "jobs-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new StudioStore(dir);
                bool ran = false;
                var jobs = new StudioJobs(store, () => dir, () => { ran = true; return "/nonexistent/python"; });
                var id = store.Add("jobs", new JsonObject { ["kind"] = "merge", ["args"] = A(("from", "x"), ("to", "main")), ["status"] = "queued", ["log"] = new JsonArray() });
                await jobs.RunAsync(id);
                var j = store.Get("jobs", id)!;
                Assert.Equal("failed", j["status"]!.ToString());
                Assert.Contains("shared base branch", j["log"]![0]!.ToString());
                Assert.False(ran);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        [Fact]
        public async Task AJobRunsSyncJobPy_AndWritesItsLogAndResultBack()
        {
            if (OperatingSystem.IsWindows()) return;   // the stand-in python is a shell script
            var dir = Path.Combine(Path.GetTempPath(), "jobs-" + Guid.NewGuid().ToString("N"));
            try
            {
                var root = Path.Combine(dir, "repo");
                Directory.CreateDirectory(Path.Combine(root, ".claude", "skills", "vessel-studio"));
                File.WriteAllText(Path.Combine(root, ".claude", "skills", "vessel-studio", "sync_job.py"), "");
                var fake = Path.Combine(dir, "python");
                File.WriteAllText(fake, "#!/bin/sh\necho \"args: $*\" >&2\necho '{\"ok\": true, \"log\": [\"a vs b: 2 ahead, 0 behind\"], \"ahead\": 2, \"behind\": 0, \"guarded\": false}'\n");
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var store = new StudioStore(Path.Combine(dir, "db"));
                var jobs = new StudioJobs(store, () => root, () => fake);
                var id = store.Add("jobs", new JsonObject { ["kind"] = "compare", ["args"] = A(("from", "a"), ("to", "b")), ["status"] = "queued", ["log"] = new JsonArray() });
                await jobs.RunAsync(id);
                var j = store.Get("jobs", id)!;
                Assert.Equal("done", j["status"]!.ToString());
                Assert.Equal(2, j["result"]!["ahead"]!.GetValue<int>());
                Assert.Contains(j["log"]!.AsArray(), l => l!.ToString().Contains("2 ahead"));
                await jobs.RunAsync(id);   // not queued any more: never runs twice
                Assert.Equal("done", store.Get("jobs", id)!["status"]!.ToString());
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }

    public class StudioServerTests : IDisposable
    {
        readonly StudioFixture _fx = new();
        readonly StudioServer _srv;
        readonly HttpClient _http = new();
        readonly List<string> _asked = new();

        public StudioServerTests()
        {
            _srv = new StudioServer(new StudioServer.Options(_fx.Data, "git", () => _fx.Repo,
                Ask: async (prompt, onText, ct) => { _asked.Add(prompt); onText("half"); await Task.Yield(); onText("half and whole"); return "half and whole"; },
                Python: () => null)).Start();
            _srv.EnsureBuilt();
        }

        public void Dispose()
        {
            var f = _srv.ServerFile;
            _srv.Dispose();
            Assert.False(File.Exists(f));   // gone when Amoebius closes
            _http.Dispose(); _fx.Dispose();
        }

        HttpRequestMessage Post(string path, string json, string? origin)
        {
            var m = new HttpRequestMessage(HttpMethod.Post, _srv.BaseUrl + path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (origin != null) m.Headers.TryAddWithoutValidation("Origin", origin);
            return m;
        }

        async Task<JsonObject> Api(string json) =>
            (JsonObject)JsonNode.Parse(await (await _http.SendAsync(Post(".amoebius/api", json, _srv.Origin))).Content.ReadAsStringAsync())!;

        [Fact]
        public async Task ServesTheBuild_WithTheBridgeFirstInHead_AndNoReferrer()
        {
            var r = await _http.GetAsync(_srv.BaseUrl + "stoat.html");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var html = await r.Content.ReadAsStringAsync();
            Assert.Contains("<HEAD lang=\"en\">" + StudioServer.BridgeTag + "\n<title>", html);
            Assert.Contains(StudioBuild.Tag, html);
            Assert.Equal("no-referrer", r.Headers.GetValues("Referrer-Policy").Single());
            Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
            var bridge = await _http.GetStringAsync(_srv.BaseUrl + ".amoebius/bridge.js");
            Assert.Contains("window.claude", bridge);
            Assert.Contains("\"repo\"", await _http.GetStringAsync(_srv.BaseUrl + "build.json"));
            Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync(_srv.BaseUrl)).StatusCode);   // the hub
        }

        [Fact]
        public async Task TheTokenIsRequired()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(_srv.Origin + "/stoat.html")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(_srv.Origin + "/" + new string('0', _srv.Token.Length) + "/stoat.html")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(_srv.Origin + "/" + _srv.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(_srv.BaseUrl + "..%2F..%2F_data%2Fuser.json")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(_srv.BaseUrl + "nope.html")).StatusCode);
            Assert.NotEqual(StudioServer.AfterToken("/" + _srv.Token + "/x", _srv.Token), StudioServer.AfterToken("/" + _srv.Token + "x/x", _srv.Token));
            Assert.Equal(48, _srv.Token.Length);
            using var other = new StudioServer(new StudioServer.Options(Path.Combine(_fx.Root, "_d2"), "git", () => _fx.Repo));
            Assert.NotEqual(_srv.Token, other.Token);   // per launch
        }

        [Fact]
        public async Task OtherOriginsAreRefused()
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(Post(".amoebius/api", "{\"op\":\"user.id\"}", "https://evil.example"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(Post(".amoebius/api", "{\"op\":\"user.id\"}", null))).StatusCode);   // no Origin: not a page
            Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(Post(".amoebius/api", "{\"op\":\"user.id\"}", "http://localhost:" + _srv.Port))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(Post(".amoebius/sample", "{\"prompt\":\"x\"}", "https://yskhan61.github.io"))).StatusCode);
            var page = new HttpRequestMessage(HttpMethod.Get, _srv.BaseUrl + "stoat.html");
            page.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");
            Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(page)).StatusCode);
            Assert.Empty(_asked);
            // the rule itself: our Host only (no DNS rebinding), our Origin only
            Assert.False(StudioServer.Allowed("evil.example:" + _srv.Port, null, null, _srv.Port, false));
            Assert.False(StudioServer.Allowed("localhost:" + _srv.Port, null, null, _srv.Port, false));
            Assert.True(StudioServer.Allowed("127.0.0.1:" + _srv.Port, null, "none", _srv.Port, false));
            Assert.True(StudioServer.Allowed("127.0.0.1:" + _srv.Port, _srv.Origin, "same-origin", _srv.Port, true));
            Assert.False(StudioServer.Allowed("127.0.0.1:" + _srv.Port, "null", null, _srv.Port, true));
        }

        [Fact]
        public async Task TheDbApi_HasTheArtifactsShapes()
        {
            Assert.True((await Api("{\"op\":\"use\",\"name\":\"db\"}"))["ok"]!.GetValue<bool>());
            Assert.Equal("not_granted", (await Api("{\"op\":\"use\",\"name\":\"artifact\"}"))["error"]!["code"]!.ToString());
            var me = (await Api("{\"op\":\"user.id\"}"))["id"]!.ToString();
            Assert.StartsWith("amoebius-", me);
            var id = (await Api("{\"op\":\"db.add\",\"path\":\"decisions\",\"data\":{\"text\":\"ship it\",\"kind\":\"decision\",\"by\":\"" + me + "\",\"at\":\"2026-10-10T02:00:00Z\"}}"))["id"]!.ToString();
            var list = await Api("{\"op\":\"db.list\",\"path\":\"decisions\",\"orderBy\":\"at\",\"dir\":\"desc\",\"limit\":60}");
            Assert.Equal(id, list["docs"]![0]!["id"]!.ToString());
            Assert.Equal("ship it", list["docs"]![0]!["data"]!["text"]!.ToString());
            Assert.Equal("ship it", (await Api("{\"op\":\"db.get\",\"path\":\"decisions\",\"id\":\"" + id + "\"}"))["data"]!["text"]!.ToString());
            var prof = await Api("{\"op\":\"user.profiles\",\"ids\":[\"" + me + "\",\"someone-else\"]}");
            Assert.NotNull(prof["profiles"]![me]!["name"]);
            Assert.Null(prof["profiles"]!["someone-else"]);
            Assert.Equal("invalid_argument", (await Api("{\"op\":\"db.add\",\"path\":\"../etc\",\"data\":{}}"))["error"]!["code"]!.ToString());
            Assert.Equal("not_found", (await Api("{\"op\":\"db.update\",\"path\":\"jobs\",\"id\":\"nope\",\"data\":{\"a\":1}}"))["error"]!["code"]!.ToString());
        }

        [Fact]
        public async Task TheSessionConnector_IsAmoebius_AndAMessageRunsTheJob()
        {
            var envs = await Api("{\"op\":\"mcp.call\",\"server\":\"Claude Code Remote\",\"tool\":\"list_environments\",\"args\":{}}");
            Assert.Equal("env_amoebius", envs["result"]!["payload"]!["environments"]![0]!["environment_id"]!.ToString());
            Assert.Equal("server_not_connected", (await Api("{\"op\":\"mcp.call\",\"server\":\"GitHub\",\"tool\":\"x\"}"))["error"]!["code"]!.ToString());
            // a merge into a guarded branch, dispatched exactly as sync.js does: refused by Amoebius, nothing runs
            var id = (await Api("{\"op\":\"db.add\",\"path\":\"jobs\",\"data\":{\"kind\":\"merge\",\"args\":{\"from\":\"x\",\"to\":\"Ys-bleeding-edge\"},\"status\":\"queued\",\"log\":[]}}"))["id"]!.ToString();
            var sent = await Api("{\"op\":\"mcp.call\",\"server\":\"Claude Code Remote\",\"tool\":\"send_message\",\"args\":{\"session_id\":\"amoebius-local\",\"message\":\"Vessel Studio Sync job " + id + " (merge) for x\"}}");
            Assert.True(sent["result"]!["payload"]!["ok"]!.GetValue<bool>());
            JsonNode? job = null;
            for (int i = 0; i < 50 && (job = _srv.Store.Get("jobs", id))!["status"]!.ToString() == "queued"; i++) await Task.Delay(100);
            Assert.Equal("failed", job!["status"]!.ToString());
            Assert.Contains("shared base branch", job["log"]!.ToJsonString());
        }

        [Fact]
        public async Task Sample_StreamsTheAnswer()
        {
            var r = await _http.SendAsync(Post(".amoebius/sample", "{\"prompt\":\"what is GM?\"}", _srv.Origin));
            var lines = (await r.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!).ToList();
            Assert.Equal("half", lines[0]["text"]!.ToString());
            Assert.True(lines[^1]["done"]!.GetValue<bool>());
            Assert.Equal("half and whole", lines[^1]["text"]!.ToString());
            Assert.Equal("what is GM?", _asked.Single());
        }

        [Fact]
        public void WithBridge_GoesFirstInHead_OrAfterTheDoctype()
        {
            Assert.Equal("<html><head>" + StudioServer.BridgeTag + "<title>", StudioServer.WithBridge("<html><head><title>"));
            Assert.Equal("<!doctype html>" + StudioServer.BridgeTag + "<p>", StudioServer.WithBridge("<!doctype html><p>"));
            Assert.StartsWith(StudioServer.BridgeTag, StudioServer.WithBridge("<p>x"));
            Assert.Equal(StudioServer.BridgeTag + "<header>", StudioServer.WithBridge("<header>"));   // <header> is not <head>
        }

        /// <summary>
        /// The bridge as a page runs it (node, when the machine has it): it finds the API beside itself. A wrong base
        /// (".amoebius/.amoebius/api") passes every server test and still leaves every page without a backend.
        /// </summary>
        [Fact]
        public async Task Bridge_CallsTheApiBesideItself()
        {
            string? node = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Select(d => Path.Combine(d, OperatingSystem.IsWindows() ? "node.exe" : "node")).FirstOrDefault(File.Exists);
            if (node == null) return;   // nothing to run it with here
            string js = await _http.GetStringAsync(_srv.BaseUrl + ".amoebius/bridge.js");
            string harness = Path.Combine(_fx.Root, "bridge-harness.js");
            File.WriteAllText(harness,
                "const seen=[];globalThis.window=globalThis;globalThis.location={href:'" + _srv.BaseUrl + "stoat.html#amoebius'};" +
                "globalThis.document={currentScript:{src:'" + _srv.BaseUrl + ".amoebius/bridge.js'}};" +
                "globalThis.fetch=async(u,o)=>{seen.push(u);return{ok:true,status:200,json:async()=>({ok:true}),text:async()=>'{}'}};\n" +
                js + "\nwindow.claude.use('db').catch(()=>{}).finally(()=>setTimeout(()=>console.log(seen.join('\\n')),50));");
            var psi = new ProcessStartInfo(node, $"\"{harness}\"") { RedirectStandardOutput = true, UseShellExecute = false };
            using var p = Process.Start(psi)!;
            string output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            Assert.Contains(_srv.BaseUrl + ".amoebius/api", output);
            Assert.DoesNotContain(".amoebius/.amoebius/", output);
        }

        [Fact]
        public void ServerFile_SaysWhereItListens_ForUnity()
        {
            var o = JsonNode.Parse(File.ReadAllText(_srv.ServerFile))!;
            Assert.Equal(_srv.Port, o["port"]!.GetValue<int>());
            Assert.Equal(_srv.Token, o["token"]!.ToString());
            Assert.Equal(Path.GetFullPath(_fx.Repo), o["root"]!.ToString());
            Assert.Equal(Environment.ProcessId, o["pid"]!.GetValue<int>());
        }

        [Fact]
        public async Task APageLoad_ShowsANewCommit()
        {
            Assert.DoesNotContain("second", await _http.GetStringAsync(_srv.BaseUrl + "stoat.html"));
            File.WriteAllText(Path.Combine(_fx.Repo, StudioBuild.Dir, "stoat.html"), "<html><head></head><body>second</body></html>");
            _fx.Git("-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qam", "second");
            Assert.Contains("second", await _http.GetStringAsync(_srv.BaseUrl + "stoat.html"));
            Assert.Equal("second", _srv.Current!.Subject);
        }

        [Fact]
        public void PageUrl_IsTheServedBuild_WithTheHostHash()
        {
            Assert.Equal(_srv.BaseUrl + "stoat.html#amoebius", _srv.PageUrl("stoat.html"));
            Assert.StartsWith("http://127.0.0.1:", _srv.PageUrl("index.html"));
        }
    }

    public class StudioAskTests
    {
        [Fact]
        public void ParseLine_ReadsAssistantTextAndErrors()
        {
            Assert.Equal("hello", StudioAsk.ParseLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"text\",\"text\":\"hello\"}]}}").text);
            Assert.Null(StudioAsk.ParseLine("{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\",\"name\":\"Read\"}]}}").text);
            var e = StudioAsk.ParseLine("{\"type\":\"result\",\"is_error\":true,\"result\":\"usage limit reached\"}");
            Assert.Equal("rate_limited", e.code);
            Assert.Null(StudioAsk.ParseLine("not json").text);
        }

        [Fact]
        public async Task Run_NeedsTheCli_AndStreamsWhatItSays()
        {
            var ex = await Assert.ThrowsAsync<StudioAsk.AskException>(() => StudioAsk.Run(null, "q", ".", null, null, _ => { }, CancellationToken.None));
            Assert.Equal("unavailable", ex.Code);
            if (OperatingSystem.IsWindows()) return;
            var dir = Path.Combine(Path.GetTempPath(), "ask-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var cli = Path.Combine(dir, "claude");
                // a stand-in claude: echoes its read-only flags and the question back as two assistant messages
                File.WriteAllText(cli, "#!/bin/sh\nq=$(cat)\necho \"{\\\"type\\\":\\\"assistant\\\",\\\"message\\\":{\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"args $*\\\"}]}}\"\n" +
                                      "echo \"{\\\"type\\\":\\\"assistant\\\",\\\"message\\\":{\\\"content\\\":[{\\\"type\\\":\\\"text\\\",\\\"text\\\":\\\"you asked $q\\\"}]}}\"\n");
                File.SetUnixFileMode(cli, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var seen = new List<string>();
                var answer = await StudioAsk.Run(cli, "GM", dir, null, null, seen.Add, CancellationToken.None);
                Assert.Contains("--permission-mode plan", answer);
                Assert.Contains("--disallowedTools Bash Edit Write", answer);
                Assert.EndsWith("you asked GM", answer);
                Assert.Equal(2, seen.Count);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
