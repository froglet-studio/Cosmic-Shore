using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Vessel Studio's Sync jobs, run by Amoebius itself (/vessel-studio D33, SKILL.md section 5). The page writes a
    /// job into the <c>jobs</c> collection and "messages the session"; on claude.ai that is a Claude Code session, here it
    /// is this runner, which does what the skill tells a session to do: run <c>sync_job.py</c> in the studio checkout and
    /// write its log and result back into the job. The SAME limits as the skill, checked here before Python runs and again
    /// by sync_job.py: never merge into or delete a shared base branch (<see cref="Guarded"/>), never delete the tools
    /// branch or the checkout's own branch, branch names by <see cref="BranchRe"/>, no force-push (sync_job.py has none),
    /// one job at a time, every job timed out.
    /// </summary>
    public sealed class StudioJobs
    {
        /// <summary>sync_job.py's GUARDED and sync.js's GUARDED: shared base branches, merged only through a pull request.</summary>
        public static readonly string[] Guarded = { "bleeding-edge", "Ys-bleeding-edge", "main", "master" };
        /// <summary>The branch the /vessel-studio skill and its scripts live on (sync.js TOOLS_REF): never deleted.</summary>
        public const string ToolsRef = "Ys-bleeding-edge";
        /// <summary>A branch name a job may name: git's own rules, narrowed (no leading dash, no "..", "//", "@{", no trailing "." or "/" or ".lock").</summary>
        public static readonly Regex BranchRe = new(@"^(?!-)(?!.*\.\.)(?!.*//)(?!.*@\{)(?!.*\.lock$)[A-Za-z0-9._/-]{1,120}(?<![./])$", RegexOptions.CultureInvariant);
        public static readonly string[] Kinds = { "refresh", "compare", "merge", "delete" };
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        readonly StudioStore _store;
        readonly Func<string?> _root;
        readonly Func<string?> _python;
        readonly Func<string, string?, bool>? _rebuild;
        readonly string _git;
        readonly SemaphoreSlim _one = new(1, 1);
        readonly HashSet<string> _started = new();

        /// <param name="store">The studio db (jobs live in its <c>jobs</c> collection).</param>
        /// <param name="root">The studio checkout (where sync_job.py and git run), or null when there is none.</param>
        /// <param name="python">Python 3, or null (Sync then says it needs Python).</param>
        /// <param name="rebuild">Refresh: rebuild the served studio from <c>origin/&lt;branch&gt;</c>; true when it now shows something new.</param>
        public StudioJobs(StudioStore store, Func<string?> root, Func<string?> python, Func<string, string?, bool>? rebuild = null, string git = "git")
        {
            _store = store; _root = root; _python = python; _rebuild = rebuild; _git = git;
        }

        public static bool IsBranch(string? b) => b != null && BranchRe.IsMatch(b);

        /// <summary>Why this job must not run, or null. The checkout's own branch is never deleted.</summary>
        public static string? Refuse(string kind, JsonObject? args, string? checkoutBranch)
        {
            string? Arg(string k) => args?[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
            switch (kind)
            {
                case "refresh":
                {
                    var b = Arg("branch");
                    if (!IsBranch(b)) return "refused: not a branch name";
                    var shown = Arg("shown");
                    if (!string.IsNullOrEmpty(shown) && !Regex.IsMatch(shown, "^[0-9a-f]{7,40}$")) return "refused: shown is not a commit";
                    return null;
                }
                case "compare":
                case "merge":
                {
                    string? from = Arg("from"), to = Arg("to");
                    if (!IsBranch(from) || !IsBranch(to)) return "refused: not a branch name";
                    if (from == to) return "refused: pick two different branches";
                    if (kind == "merge" && Guarded.Contains(to)) return $"refused: {to} is a shared base branch; merge it through a pull request";
                    return null;
                }
                case "delete":
                {
                    var b = Arg("branch");
                    if (!IsBranch(b)) return "refused: not a branch name";
                    if (Guarded.Contains(b) || b == ToolsRef) return $"refused: {b} cannot be deleted from the panel";
                    if (b == checkoutBranch) return $"refused: {b} is the branch this checkout is on";
                    return null;
                }
                default: return $"refused: unknown job kind \"{kind}\"";
            }
        }

        /// <summary>The sync_job.py arguments for a job that passed <see cref="Refuse"/>.</summary>
        public static List<string> Args(string kind, JsonObject args, string? checkoutBranch)
        {
            string A(string k) => args[k]!.GetValue<string>().Trim();
            var list = new List<string>();
            switch (kind)
            {
                case "refresh":
                    list.AddRange(new[] { "status", "--branch", A("branch") });
                    if (args["shown"] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0) list.AddRange(new[] { "--shown", s.Trim() });
                    break;
                case "compare": list.AddRange(new[] { "compare", "--from", A("from"), "--to", A("to") }); break;
                case "merge": list.AddRange(new[] { "merge", "--from", A("from"), "--to", A("to"), "--yes" }); break;   // the page's Confirm merge is the yes
                case "delete":
                    list.AddRange(new[] { "delete", "--branch", A("branch"), "--yes", "--keep", ToolsRef });
                    if (!string.IsNullOrEmpty(checkoutBranch)) list.AddRange(new[] { "--keep", checkoutBranch });
                    break;
            }
            return list;
        }

        /// <summary>The page "messaged the session" about job <paramref name="id"/>: run it once, in the background.</summary>
        public Task? Kick(string id)
        {
            lock (_started) if (!_started.Add(id)) return null;
            return Task.Run(() => RunAsync(id));
        }

        public async Task RunAsync(string id)
        {
            var job = _store.Get("jobs", id);
            if (job == null) return;
            if ((job["status"] as JsonValue)?.GetValue<string>() != "queued") return;
            string kind = job["kind"] is JsonValue kv && kv.TryGetValue<string>(out var k) ? k : "";
            var args = job["args"] as JsonObject ?? new JsonObject();
            var log = new JsonArray();
            void Write(string status, JsonObject? result = null)
            {
                var patch = new JsonObject { ["status"] = status, ["log"] = log.DeepClone(), ["updatedAt"] = DateTime.UtcNow.ToString("o") };
                if (result != null) patch["result"] = result;
                _store.Update("jobs", id, patch);
            }

            await _one.WaitAsync();
            try
            {
                var root = _root();
                string? branch = root == null ? null : CurrentBranch(_git, root);
                var refuse = Refuse(kind, args, branch);
                if (refuse != null) { log.Add(refuse); Write("failed", new JsonObject { ["refused"] = true }); return; }
                if (root == null) { log.Add("no studio checkout: open Amoebius from Unity or set your Cosmic Shore folder in OPTIONS"); Write("failed"); return; }
                var py = _python();
                var script = Path.Combine(root, ".claude", "skills", "vessel-studio", "sync_job.py");
                if (py == null) { log.Add("Sync needs Python 3 on this computer (python.org), then try again"); Write("failed"); return; }
                if (!File.Exists(script)) { log.Add($"this checkout has no {Path.GetRelativePath(root, script)}: switch it to {ToolsRef}"); Write("failed"); return; }
                log.Add($"Amoebius runs {kind} in {root}");
                Write("running");

                var (code, stdout, stderr) = await RunPython(py, script, Args(kind, args, branch), root, _git);
                JsonObject? res = null;
                try { res = JsonNode.Parse(stdout.Trim()) as JsonObject; } catch (JsonException) { }
                if (res == null)
                {
                    log.Add(code == -1 ? $"{kind} timed out after {Timeout.TotalMinutes:0} minutes" : $"sync_job.py gave no result (exit {code})");
                    foreach (var l in stderr.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(6)) log.Add("  " + l.Trim());
                    Write("failed"); return;
                }
                if (res["log"] is JsonArray lines) foreach (var l in lines) log.Add(l?.ToString() ?? "");
                bool ok = res["ok"] is JsonValue okv && okv.TryGetValue<bool>(out var b) && b;
                var result = new JsonObject();
                foreach (var kvp in res) if (kvp.Key is not "ok" and not "log") result[kvp.Key] = kvp.Value?.DeepClone();
                if (ok && kind == "refresh" && _rebuild != null && result["upToDate"] is JsonValue u && u.TryGetValue<bool>(out var up) && !up)
                {
                    var target = args["branch"]!.GetValue<string>().Trim();
                    try
                    {
                        bool changed = _rebuild(target, result["pathSha"]?.ToString());
                        result["published"] = changed;
                        log.Add(changed ? $"rebuilt the studio from origin/{target}" : "the studio already shows that commit");
                    }
                    catch (Exception e) { log.Add("rebuild failed: " + e.Message); ok = false; }
                }
                Write(ok ? "done" : "failed", result);
            }
            catch (Exception e)
            {
                log.Add("job failed: " + e.Message);
                try { Write("failed"); } catch { }
            }
            finally { _one.Release(); }
        }

        static string? CurrentBranch(string git, string root)
        {
            var b = ProcessRunner.Capture(git, "-C", root, "rev-parse", "--abbrev-ref", "HEAD");
            return b is { Length: > 0 } && b != "HEAD" ? b : null;
        }

        static async Task<(int code, string stdout, string stderr)> RunPython(string py, string script, List<string> args, string cwd, string git)
        {
            var parts = py.Split('\u0001');   // "py\u0001-3" = the Windows launcher with its version flag
            var psi = new ProcessStartInfo(parts[0])
            {
                WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var a in parts.Skip(1)) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add(script);
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";   // a push that needs a password fails, never hangs
            psi.Environment["PYTHONIOENCODING"] = "utf-8";
            if (Path.IsPathRooted(git) && Path.GetDirectoryName(git) is { } gd)   // sync_job.py runs "git": the one Amoebius uses
                psi.Environment["PATH"] = gd + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? "");
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(Timeout);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return (-1, "", await e);
            }
            return (p.ExitCode, await o, await e);
        }

        /// <summary>Python 3: PRISMA_PYTHON, else python3 / python on PATH, else the Windows py launcher (as "py\u0001-3").</summary>
        public static string? FindPython()
        {
            var env = Environment.GetEnvironmentVariable("PRISMA_PYTHON");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
            var names = OperatingSystem.IsWindows() ? new[] { "python3.exe", "python.exe" } : new[] { "python3", "python" };
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                foreach (var n in names)
                {
                    if (dir.Length == 0) continue;
                    var f = Path.Combine(dir, n);
                    // the Microsoft Store stub (WindowsApps) opens the Store instead of running: skip it
                    if (File.Exists(f) && !f.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)
                        && ProcessRunner.Capture(f, "-c", "import sys; print(sys.version_info[0])") == "3") return f;
                }
            if (OperatingSystem.IsWindows() && ProcessRunner.Capture("py", "-3", "-c", "print(3)") == "3") return "py\u0001-3";
            return null;
        }
    }
}
