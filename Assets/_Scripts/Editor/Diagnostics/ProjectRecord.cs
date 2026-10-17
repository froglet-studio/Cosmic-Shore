using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CosmicShore.Editor.Froglet;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The data behind the after-play Project Record (<see cref="BugHuntSendConfirmation"/>): what this machine has sent
    /// to Claude, and the performance status each branch on the performance path publishes. Read-only: it fetches and
    /// reads git objects, and never touches the working tree, the index or the current branch.
    ///
    /// <para><b>Branches.</b> The list and each branch's role come from <c>Docs/perf/status.json</c>'s
    /// <c>branchFlow</c> (written by <c>Tools/Build/perf_hunt.py</c>: Ys-bleeding-edge first, where the performance
    /// work lands, then bleeding-edge). For each branch it fetches <c>origin/&lt;branch&gt;</c> and reads that branch's
    /// own <c>Docs/perf/status.json</c>, the file the daily sweep and every performance session write.</para>
    ///
    /// <para><b>This machine's sends.</b> <c>origin/bugledger/&lt;user&gt;</c>, the branch <see cref="BugLedgerSender"/>
    /// pushes to: its recent commits and the report files on it, grouped by kind (perf, net, flights, redlog, crash).
    /// A load waits for a running send to finish before it fetches anything, so two git fetches never race.</para>
    ///
    /// <para>One load at a time, on a background thread (<see cref="FrogletGit"/> is Process-based and safe off-thread
    /// once its statics are resolved on the main thread, as <see cref="BugLedgerSender"/> does).</para>
    /// </summary>
    public sealed class ProjectRecord
    {
        public sealed class BranchStatus
        {
            public string Branch = "";
            public string Role = "";
            public bool Found;
            public string Error = "";
            public string GeneratedUtc = "";
            public string LastChange = "";            // the last commit that changed the status file
            public bool GatesOk;
            public readonly List<string> RedGates = new();
            public readonly List<string> Regressions = new();
            public readonly List<string> Next = new();
            public readonly List<string> Amoebius = new();
            public readonly List<Capture> Captures = new();
            public readonly List<string> OwedScenarios = new();
            public readonly List<string> Tasks = new();
            public int BugsOpen, BugsValidating;
            public readonly List<string> BugTitles = new();
        }

        public sealed class Capture
        {
            public string Key = "", User = "", When = "", Verdict = "";
            public double CpuMs, GcKB;
            public bool Valid;
        }

        public sealed class KindFiles
        {
            public int Count;
            public readonly List<string> Newest = new();   // file names, newest first
        }

        public sealed class Send
        {
            public string When = "", Author = "", Subject = "";
        }

        public readonly List<BranchStatus> Branches = new();
        public readonly List<Send> Sends = new();
        public readonly Dictionary<string, KindFiles> Files = new(StringComparer.Ordinal);
        public string User = "";
        public string SendBranch = "";
        public DateTime LoadedLocal;
        public string Problem = "";

        // ── Loading ─────────────────────────────────────────────────────────────────────────────

        static int _loading;
        static volatile ProjectRecord _latest;

        /// <summary>The last finished load, or null before the first.</summary>
        public static ProjectRecord Latest => _latest;

        public static bool IsLoading => Volatile.Read(ref _loading) == 1;

        sealed class Plan
        {
            public string User, SendBranch, ReportsDir;
            public List<(string branch, string role)> Flow;
        }

        /// <summary>Main thread: start a background load unless one is running. Never throws.</summary>
        public static void LoadInBackground()
        {
            if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0) return;
            Plan plan;
            try
            {
                if (!FrogletGit.IsAvailable) throw new InvalidOperationException("git is not available in this project");
                _ = FrogletGit.TopLevel;
                string user = Environment.UserName;
                string shared = BugLedger.RootDir != null
                    ? FrogletGit.ToRepoRelative(Path.Combine(BugLedger.RootDir, "shared"))
                    : "BugLedger/shared";
                plan = new Plan
                {
                    User = user,
                    SendBranch = BugLedgerSender.BranchFor(user),
                    ReportsDir = shared + "/reports/" + BugHuntReports.Slug(user) + "/",
                    Flow = ReadFlow(),
                };
            }
            catch (Exception e)
            {
                _latest = new ProjectRecord { Problem = $"{e.GetType().Name}: {e.Message}", LoadedLocal = DateTime.Now };
                Interlocked.Exchange(ref _loading, 0);
                return;
            }

            new Thread(() =>
            {
                var record = new ProjectRecord { User = plan.User, SendBranch = plan.SendBranch };
                try { record.Gather(plan); }
                catch (Exception e) { record.Problem = $"{e.GetType().Name}: {e.Message}"; }
                record.LoadedLocal = DateTime.Now;
                _latest = record;
                Interlocked.Exchange(ref _loading, 0);
            })
            { Name = "ProjectRecord.Load", IsBackground = true }.Start();
        }

        /// <summary>The branch flow from this checkout's status file; the two known branches when it has none.</summary>
        static List<(string, string)> ReadFlow()
        {
            var flow = new List<(string, string)>();
            try
            {
                var path = Path.Combine(FrogletGit.TopLevel, "Docs", "perf", "status.json");
                if (File.Exists(path) && JObject.Parse(File.ReadAllText(path))["branchFlow"] is JArray arr)
                    foreach (var item in arr)
                    {
                        var branch = (string)item["branch"];
                        if (!string.IsNullOrEmpty(branch)) flow.Add((branch, (string)item["role"] ?? ""));
                    }
            }
            catch { /* fall back below */ }
            if (flow.Count == 0)
            {
                flow.Add(("Ys-bleeding-edge", "Performance work lands here first"));
                flow.Add(("bleeding-edge", "The team's shared branch"));
            }
            return flow;
        }

        void Gather(Plan plan)
        {
            // Right after Play the send is fetching and pushing; two git fetches at once can trip over each other's
            // ref locks, so let it finish first (it takes seconds; give up waiting after a minute).
            for (int waited = 0; BugLedgerSender.IsBusy && waited < 600; waited++) Thread.Sleep(100);

            foreach (var (branch, role) in plan.Flow)
                Branches.Add(ReadBranch(branch, role));

            string sendRef = "refs/remotes/origin/" + plan.SendBranch;
            if (!BugLedgerSender.IsBusy)
                FrogletGit.Run("fetch", "-q", "origin", "+refs/heads/" + plan.SendBranch + ":" + sendRef);   // a missing branch just fails: nothing sent yet

            var log = FrogletGit.Run("log", "-15", "--format=%cI%x09%an%x09%s", sendRef);
            if (log.Ok)
                foreach (var line in log.StdOut.Split('\n'))
                {
                    var parts = line.Trim().Split('\t');
                    if (parts.Length < 3) continue;
                    Sends.Add(new Send { When = ShortTime(parts[0]), Author = parts[1], Subject = parts[2] });
                }

            var files = FrogletGit.Run("ls-tree", "-r", "--name-only", sendRef, "--", plan.ReportsDir);
            if (!files.Ok) return;
            var byKind = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var raw in files.StdOut.Split('\n'))
            {
                var path = raw.Trim();
                if (!path.StartsWith(plan.ReportsDir, StringComparison.Ordinal)) continue;
                var rest = path.Substring(plan.ReportsDir.Length);
                int slash = rest.IndexOf('/');
                if (slash <= 0) continue;
                string kind = rest.Substring(0, slash), name = rest.Substring(slash + 1);
                if (!byKind.TryGetValue(kind, out var list)) byKind[kind] = list = new List<string>();
                list.Add(name);
            }
            foreach (var (kind, names) in byKind)
            {
                // The file names carry their own yyyyMMdd_HHmmss stamp, so a reverse name sort is newest first
                // within a family; good enough for "the latest few".
                names.Sort((a, b) => string.CompareOrdinal(StampOf(b), StampOf(a)));
                var kf = new KindFiles { Count = names.Count };
                for (int i = 0; i < names.Count && i < 8; i++) kf.Newest.Add(names[i]);
                Files[kind] = kf;
            }
        }

        static BranchStatus ReadBranch(string branch, string role)
        {
            var st = new BranchStatus { Branch = branch, Role = role };
            string remoteRef = "refs/remotes/origin/" + branch;
            FrogletGit.Run("fetch", "-q", "origin", "+refs/heads/" + branch + ":" + remoteRef);
            var show = FrogletGit.Run("show", remoteRef + ":Docs/perf/status.json");
            if (!show.Ok)
            {
                st.Error = "no Docs/perf/status.json on origin/" + branch + " yet";
                return st;
            }

            var changed = FrogletGit.Run("log", "-1", "--format=%cI%x09%an", remoteRef, "--", "Docs/perf/status.json");
            if (changed.Ok)
            {
                var p = changed.StdOut.Trim().Split('\t');
                if (p.Length >= 2) st.LastChange = $"{ShortTime(p[0])} by {p[1]}";
            }

            try
            {
                var j = JObject.Parse(show.StdOut);
                st.Found = true;
                st.GeneratedUtc = ShortTime((string)j["generatedUtc"] ?? "");
                st.GatesOk = (bool?)j["gatesOk"] ?? false;
                if (j["gates"] is JArray gates)
                    foreach (var g in gates)
                        if (!((bool?)g["ok"] ?? true)) st.RedGates.Add((string)g["gate"] ?? "?");
                AddStrings(j["regressions"], st.Regressions);
                AddStrings(j["next"], st.Next);
                if (j["amoebius"] is JArray amo)
                    foreach (var a in amo)
                    {
                        string top = "";
                        if (a["topGameAllocators"] is JArray alloc)
                            for (int i = 0; i < alloc.Count && i < 3; i++)
                                if (alloc[i] is JArray pair && pair.Count >= 2)
                                    top += (top.Length > 0 ? ", " : "") + $"{(string)pair[0]} {(double)pair[1]:0.0} KB/f";
                        st.Amoebius.Add($"{(string)a["key"]}: {(string)a["verdict"]}" + (top.Length > 0 ? $"  (top: {top})" : ""));
                    }
                if (j["captures"] is JArray caps)
                    foreach (var c in caps)
                        st.Captures.Add(new Capture
                        {
                            Key = (string)c["key"] ?? "?",
                            User = (string)c["user"] ?? "?",
                            When = (string)c["timestamp"] ?? "",
                            Verdict = (string)c["verdict"] ?? "",
                            Valid = (bool?)c["valid"] ?? false,
                            CpuMs = (double?)c["cpuBusyMs"] ?? 0,
                            GcKB = (double?)c["gcKB"] ?? 0,
                        });
                if (j["scenarios"] is JArray scen)
                    foreach (var s in scen)
                        if ((bool?)s["owed"] ?? false) st.OwedScenarios.Add((string)s["title"] ?? (string)s["label"] ?? "?");
                if (j["openPerformanceTasks"] is JArray tasks)
                    foreach (var t in tasks)
                        st.Tasks.Add($"{(string)t["id"]} ({(string)t["kind"]}): {(string)t["task"]}");
                if (j["bugs"] is JObject bugs)
                {
                    st.BugsOpen = (int?)bugs["open"] ?? 0;
                    st.BugsValidating = (int?)bugs["validating"] ?? 0;
                    if (bugs["top"] is JArray top)
                        foreach (var b in top)
                            st.BugTitles.Add($"{(string)b["id"]} [{(string)b["state"]}] {(string)b["title"]}");
                }
            }
            catch (Exception e)
            {
                st.Found = false;
                st.Error = "unreadable status.json: " + e.Message;
            }
            return st;
        }

        static void AddStrings(JToken token, List<string> into)
        {
            if (token is not JArray arr) return;
            foreach (var t in arr)
            {
                var s = (string)t;
                if (!string.IsNullOrEmpty(s)) into.Add(s);
            }
        }

        /// <summary>"2026-10-17T08:48:58Z" or "...+00:00" as local "10-17 09:48".</summary>
        static string ShortTime(string iso)
        {
            if (DateTimeOffset.TryParse(iso, out var t)) return t.ToLocalTime().ToString("MM-dd HH:mm");
            return iso;
        }

        /// <summary>The yyyyMMdd_HHmmss stamp inside a report's file name, or the name itself.</summary>
        static string StampOf(string name)
        {
            for (int i = 0; i + 15 <= name.Length; i++)
                if (char.IsDigit(name[i]) && name[i + 8] == '_' && IsDigits(name, i, 8) && IsDigits(name, i + 9, 6))
                    return name.Substring(i, 15);
            return name;
        }

        static bool IsDigits(string s, int start, int count)
        {
            for (int i = start; i < start + count; i++)
                if (i >= s.Length || !char.IsDigit(s[i])) return false;
            return true;
        }
    }
}
