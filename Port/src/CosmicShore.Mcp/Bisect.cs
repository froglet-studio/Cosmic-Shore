using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Prisma.Bisect;

namespace CosmicShore.Mcp
{
    /// <summary>
    /// prisma_bisect (ROADMAP C1b): which commit broke it. <c>git bisect run</c> over the commits
    /// between good and bad that touch Port/, in a scratch git worktree (the checkout it was asked
    /// from never moves), each candidate built there and judged by this server in
    /// <c>--bisect-step</c> mode: an engine_smoke error signature or a replay diff (engine_parity).
    /// </summary>
    public sealed partial class Tools
    {
        public static JsonObject BisectTool()
        {
            var t = Tool("prisma_bisect",
                "Find the commit that broke the engine: git bisect run over the commits between good and bad that touch Port/, each built in a scratch git worktree " +
                "(this checkout never moves) and judged by an engine_smoke error signature (check=smoke) or a replay diff (check=parity, against the good commit's own " +
                "replay by default, since Unity goldens may be missing). A candidate that does not build, or predates the replay flags, is skipped. Verifies that bad " +
                "fails and good passes first. Slow: one engine build per step.",
                new JsonObject
                {
                    ["good"] = P("string", "a commit where the check passes"),
                    ["bad"] = P("string", "a commit where it fails (default HEAD)"),
                    ["check"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("smoke", "parity"), ["description"] = "smoke (default): engine_smoke; parity: replay diff" },
                    ["signature"] = P("string", "smoke: the regression's error text - a substring, or /regex/ - matched against every error, exception, assert and crash. Without one any smoke FAIL is bad"),
                    ["scene"] = P("string", "smoke: start in this scene"),
                    ["expect"] = P("string", "smoke: scene that must be reached"),
                    ["frames"] = P("integer", "smoke: frames to run (default 300)"),
                    ["ignore"] = P("string", "smoke: '|'-separated substrings of errors to ignore"),
                    ["case"] = P("string", "parity: one case name, 'random', or 'all' (default)"),
                    ["against"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("good", "goldens"), ["description"] = "parity: good (default): the good commit's replay; goldens: Port/parity/goldens (or golden_dir)" },
                    ["golden_dir"] = P("string", "parity, against=goldens: diff against this directory"),
                    ["verify"] = P("boolean", "judge bad (and good, for smoke) before bisecting (default true)"),
                    ["repo"] = P("string", "the git checkout to bisect (default this one)"),
                }, "good");
            // It writes git state: a scratch worktree and bisect refs (both removed at the end).
            t["annotations"]!["readOnlyHint"] = false;
            return t;
        }

        static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "froglet-mcp", "bisect");

        /// <summary>LFS holds only the FMOD Android/Linux libraries, which neither check loads (audio is off): a scratch checkout skips them.</summary>
        static readonly Dictionary<string, string> GitEnv = new() { ["GIT_LFS_SKIP_SMUDGE"] = "1" };

        static async Task<RunResult> Git(string cwd, params string[] args) => await Run("git", args, cwd, TimeSpan.FromMinutes(30), GitEnv);

        static async Task<string> GitOk(string cwd, params string[] args)
        {
            var r = await Git(cwd, args);
            if (r.ExitCode != 0) throw new ToolException($"git {string.Join(' ', args)} failed ({r.ExitCode}):\n{r.Output.Trim()}");
            return r.Output.Trim();
        }

        async Task<string> Bisect(JsonObject a)
        {
            var sw = Stopwatch.StartNew();
            string repo = Str(a, "repo").Length > 0 ? Path.GetFullPath(Str(a, "repo"), _repo) : _repo;
            string check = Str(a, "check", "smoke");
            if (check is not ("smoke" or "parity")) throw new ToolException("check must be smoke or parity");
            if (Str(a, "good").Length == 0) throw new ToolException("give good: a commit where the check passes");
            string good = (await GitOk(repo, "rev-parse", "--verify", Str(a, "good") + "^{commit}")).Split('\n').Last();
            string bad = (await GitOk(repo, "rev-parse", "--verify", Str(a, "bad", "HEAD") + "^{commit}")).Split('\n').Last();
            if ((await Git(repo, "merge-base", "--is-ancestor", good, bad)).ExitCode != 0)
                return $"FAIL: good {good[..10]} is not an ancestor of bad {bad[..10]}";
            int candidates = int.Parse((await GitOk(repo, "rev-list", "--count", $"{good}..{bad}", "--", "Port/")).Split('\n').Last());
            if (candidates == 0) return $"FAIL: no commit between {good[..10]} and {bad[..10]} touches Port/";

            var root = Path.Combine(TempRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            var wt = Path.Combine(root, "wt");
            Directory.CreateDirectory(root);
            string parityRoot = _parityRoot ?? Path.Combine(_repo, "Port", "parity");
            var cfg = new JsonObject
            {
                ["check"] = check,
                ["signature"] = Str(a, "signature"),
                ["scene"] = Str(a, "scene"),
                ["expect"] = Str(a, "expect"),
                ["frames"] = Int(a, "frames", 300),
                ["ignore"] = Str(a, "ignore"),
                ["case"] = Str(a, "case", "all"),
                ["parity_root"] = parityRoot,
                ["golden_dir"] = Str(a, "golden_dir").Length > 0 ? Path.GetFullPath(Str(a, "golden_dir"), _repo) : Path.Combine(parityRoot, "goldens"),
                ["log"] = Path.Combine(root, "steps.tsv"),
            };
            var report = new StringBuilder();
            report.AppendLine($"bisect {good[..10]}..{bad[..10]}: {candidates} commit(s) touch Port/ (of {(await GitOk(repo, "rev-list", "--count", $"{good}..{bad}")).Split('\n').Last()}); check {check}" +
                              (check == "smoke" && Str(a, "signature").Length > 0 ? $" signature '{Str(a, "signature")}'" : ""));
            report.AppendLine("worktree: " + wt);
            bool started = false;
            try
            {
                await GitOk(repo, "worktree", "add", "--detach", wt, good);
                var step = new Tools(wt, parityRoot);

                if (check == "parity" && Str(a, "against", "good") == "good")
                {
                    // The good commit's own replay is the golden: run it twice so a nondeterministic
                    // case cannot pose as a regression later.
                    var baseline = Path.Combine(root, "baseline");
                    var r = await step.ParityRun(new JsonObject { ["against"] = "self", ["case"] = cfg["case"]!.GetValue<string>(), ["build"] = true });
                    if (r.BuildFailed || r.Fails > 0 || r.Work.Length == 0)
                        return $"FAIL: the good commit cannot be the baseline (it must build and replay deterministically)\n{r.Text}";
                    foreach (var d in Directory.GetDirectories(r.Work))
                    {
                        var name = Path.GetFileName(d);
                        if (name.EndsWith("-0")) continue;
                        var src = Directory.Exists(Path.Combine(d, "run")) ? Path.Combine(d, "run") : d;
                        CopyDir(src, Path.Combine(baseline, name));
                    }
                    cfg["golden_dir"] = baseline;
                    report.AppendLine($"baseline: the good commit's replay ({string.Join(", ", Directory.GetDirectories(baseline).Select(Path.GetFileName))}), deterministic over 2 runs");
                }
                await File.WriteAllTextAsync(Path.Combine(root, "step.json"), cfg.ToJsonString());

                if (Bool(a, "verify", true))
                {
                    await GitOk(wt, "checkout", "--detach", "--force", bad);
                    var (code, detail) = await step.Judge(cfg);
                    report.AppendLine($"verify bad  {bad[..10]}: {BisectVerdict.Name(code)} - {detail}");
                    if (code != BisectVerdict.Bad) return $"FAIL: bad {bad[..10]} does not show the regression ({BisectVerdict.Name(code)})\n" + report;
                    if (check == "smoke")
                    {
                        await GitOk(wt, "checkout", "--detach", "--force", good);
                        (code, detail) = await step.Judge(cfg);
                        report.AppendLine($"verify good {good[..10]}: {BisectVerdict.Name(code)} - {detail}");
                        if (code != BisectVerdict.Good) return $"FAIL: good {good[..10]} does not pass the check ({BisectVerdict.Name(code)})\n" + report;
                    }
                }

                await GitOk(wt, "bisect", "start", bad, good, "--", "Port/");
                started = true;
                var self = typeof(Tools).Assembly.Location;
                var run = await Run("git", new[] { "bisect", "run", "dotnet", "exec", self, "--repo", wt, "--bisect-step", Path.Combine(root, "step.json") },
                                    wt, TimeSpan.FromHours(8), GitEnv);
                var log = (await Git(wt, "bisect", "log")).Output;
                var outcome = BisectOutcome.Parse(run.Output, log);
                var details = File.Exists(cfg["log"]!.GetValue<string>())
                    ? File.ReadAllLines(cfg["log"]!.GetValue<string>()).Select(l => l.Split('\t')).Where(p => p.Length >= 3).GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.Last()[2])
                    : new Dictionary<string, string>();

                var head = new StringBuilder();
                if (outcome.FirstBad != null)
                {
                    var show = await GitOk(repo, "show", "--stat", "--format=%H%n%an, %ad%n%s", "--date=short", outcome.FirstBad, "--", "Port/");
                    head.AppendLine($"FOUND {outcome.FirstBad[..10]} in {outcome.Steps.Count} step(s) ({outcome.Skipped.Count()} skipped), {sw.Elapsed.TotalMinutes:0.0} min");
                    head.AppendLine(show);
                }
                else if (outcome.Candidates.Count > 0)
                    head.AppendLine($"FOUND one of {outcome.Candidates.Count} commits (the rest were skipped - they do not build or cannot be judged) in {outcome.Steps.Count} step(s):\n  " +
                                    string.Join("\n  ", outcome.Candidates));
                else
                    return $"FAIL: git bisect run gave no answer (exit {run.ExitCode})\n" + report + string.Join('\n', run.Output.Split('\n').TakeLast(40));
                head.AppendLine();
                head.Append(report);
                head.AppendLine("steps:");
                int n = 0;
                foreach (var s in outcome.Steps)
                    head.AppendLine($"  {++n,2}. {s.Verdict,-4} {s.Commit[..10]} {s.Subject}" + (details.TryGetValue(s.Commit, out var d) ? $"  -  {d}" : ""));
                return head.ToString().TrimEnd();
            }
            finally
            {
                if (started) await Git(wt, "bisect", "reset");
                if (Directory.Exists(wt)) await Git(repo, "worktree", "remove", "--force", wt);
                await Git(repo, "worktree", "prune");
            }
        }

        static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
        }

        /// <summary>Judge the commit checked out in this server's repo (the scratch worktree).</summary>
        async Task<(int Code, string Detail)> Judge(JsonObject cfg)
        {
            try
            {
                if (cfg["check"]!.GetValue<string>() == "parity")
                {
                    // A player from before the replay flags would ignore them and run until the timeout.
                    var program = Path.Combine(PortSrc("CosmicShore.Player"), "Program.cs");
                    bool capable = File.Exists(program) && (await File.ReadAllTextAsync(program)).Contains("\"--replay\"");
                    if (!capable) return (BisectVerdict.Skip, "this player has no --replay");
                    var r = await ParityRun(new JsonObject
                    {
                        ["case"] = cfg["case"]!.GetValue<string>(), ["golden_dir"] = cfg["golden_dir"]!.GetValue<string>(), ["build"] = true,
                    });
                    var failed = r.Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("FAIL", StringComparison.Ordinal) && !l.StartsWith("FAIL  -"))
                                 ?? r.Text.Split('\n')[0];
                    return (BisectVerdict.Parity(r.BuildFailed, true, r.Fails), r.BuildFailed ? "does not build" : r.Fails > 0 ? failed : r.Text.Split('\n')[0]);
                }
                else
                {
                    string sig = cfg["signature"]!.GetValue<string>();
                    var r = await SmokeRun(new JsonObject
                    {
                        ["frames"] = cfg["frames"]!.GetValue<int>(), ["scene"] = cfg["scene"]!.GetValue<string>(),
                        ["expect"] = cfg["expect"]!.GetValue<string>(), ["ignore"] = cfg["ignore"]!.GetValue<string>(), ["build"] = true,
                    });
                    int code = BisectVerdict.Smoke(r.BuildFailed, r.Pass, r.Problems, sig);
                    var hit = sig.Length > 0 ? r.Problems.FirstOrDefault(p => BisectVerdict.Matches(sig, p)) : null;
                    var detail = r.BuildFailed ? "does not build" : hit != null ? "signature: " + hit.Split('\n')[0] : r.Text.Split('\n')[0];
                    return (code, detail.Length > 160 ? detail[..160] + "..." : detail);
                }
            }
            catch (Exception e) { return (BisectVerdict.Abort, e.GetType().Name + ": " + e.Message); }
        }

        /// <summary>
        /// <c>prisma-mcp --repo WT --bisect-step step.json</c>: what <c>git bisect run</c> calls for each
        /// candidate. The exit code is the verdict; a line per step goes to the step log for the report.
        /// </summary>
        public static async Task<int> BisectStep(string repo, string cfgPath)
        {
            JsonObject cfg;
            try { cfg = JsonNode.Parse(await File.ReadAllTextAsync(cfgPath))!.AsObject(); }
            catch (Exception e) { Console.WriteLine("prisma_bisect step: unreadable " + cfgPath + ": " + e.Message); return BisectVerdict.Abort; }
            var sha = (await Git(repo, "rev-parse", "HEAD")).Output.Trim().Split('\n').Last();
            using var tools = new Tools(repo, cfg["parity_root"]?.GetValue<string>());
            var sw = Stopwatch.StartNew();
            var (code, detail) = await tools.Judge(cfg);
            detail = $"{detail} ({sw.Elapsed.TotalSeconds:0} s)";
            Console.WriteLine($"prisma_bisect step {sha[..Math.Min(10, sha.Length)]}: {BisectVerdict.Name(code)} - {detail}");
            if (cfg["log"]?.GetValue<string>() is { Length: > 0 } log)
                await File.AppendAllTextAsync(log, $"{sha}\t{BisectVerdict.Name(code)}\t{detail.Replace('\t', ' ').Replace('\n', ' ')}\n");
            return code;
        }
    }
}
