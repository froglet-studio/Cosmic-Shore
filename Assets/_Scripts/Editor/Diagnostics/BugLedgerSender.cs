using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CosmicShore.Editor.Froglet;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Sends this machine's Bug Ledger to its own branch, <c>bugledger/&lt;user&gt;</c>, and brings the
    /// fixes Claude marked there back — the plumbing behind the one "Send to Claude" button
    /// (<see cref="BugHuntAutoSend"/>). There is no UI here and nothing to configure.
    ///
    /// <para><b>Never touches your work.</b> The commit is built in a THROWAWAY git index
    /// (<c>GIT_INDEX_FILE</c>) from blobs written straight into the object store, then pushed to
    /// <c>refs/heads/bugledger/&lt;user&gt;</c>. Your working tree, your staged changes and your current
    /// branch are never read or written, so there is nothing to pull first and nothing that can
    /// conflict with what you are doing. The branch holds only <c>BugLedger/shared/</c>:
    /// <c>issues/&lt;id&gt;.bug.json</c> (this machine's live issues — a closed one simply disappears)
    /// and <c>reports/&lt;user&gt;/&lt;kind&gt;/</c> (redlog, crash and perf files; append-only).</para>
    ///
    /// <para><b>Fixes come back the same way.</b> Claude (<c>bug_ledger.py mark-fixed --commit</c>)
    /// rewrites an issue on this branch as VALIDATING with the commit that fixes it. Every sync
    /// (editor load, and before every send) reads the branch and adopts such a fix — but only once
    /// that commit is in this machine's HEAD, so play runs on code without the fix never credit
    /// it. Until then the send keeps Claude's claim on the branch instead of overwriting it.</para>
    ///
    /// <para>One operation at a time, on a background thread; <see cref="FrogletGit"/> is
    /// Process-based and safe off-thread once <see cref="Warm"/> has resolved its statics on the
    /// main thread. Results go to the Console as one line.</para>
    /// </summary>
    public static class BugLedgerSender
    {
        static int _busy;

        public static bool IsBusy => Volatile.Read(ref _busy) == 1;

        public static string BranchFor(string user) => "bugledger/" + BugHuntReports.Slug(user);

        /// <summary>Background: adopt any fix Claude published for this machine. Called at editor load.</summary>
        public static void SyncFixesInBackground()
        {
            if (!TryBegin(out var ctx)) return;
            Start("BugLedger.Sync", () =>
            {
                var branch = Fetch(ctx);
                if (branch.Parent != null) AdoptFixes(branch);
            });
        }

        /// <summary>Background: adopt fixes, then push this machine's issues and new report files.</summary>
        public static void SendInBackground()
        {
            if (!TryBegin(out var ctx)) return;
            Start("BugLedger.Send", () =>
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var outcome = SendOnce(ctx, attempt);
                    if (outcome == null) return;                      // done (sent or nothing new)
                    if (attempt == 1)
                        Debug.LogWarning($"[BugLedger] Could not send the errors to Claude: {outcome}. " +
                                         "They stay on this machine and go with the next send.");
                }
            });
        }

        // ── Internals ────────────────────────────────────────────────────────────────────────────

        sealed class Context
        {
            public string User;
            public string Branch;
            public string RemoteRef;
            public string IssuesDir;     // repo-relative, e.g. "BugLedger/shared/issues"
            public string ReportsDir;    // repo-relative, e.g. "BugLedger/shared/reports"
            public string IndexPath;     // throwaway index under Library/
            public string TempDir;       // blobs are staged from here
            public List<BugHuntReports.Report> ReportCandidates;
        }

        sealed class RemoteBranch
        {
            public string Parent;                                         // null: branch does not exist yet
            public readonly Dictionary<string, BugLedgerIssue> Issues = new(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> ReportPaths = new(StringComparer.Ordinal);
        }

        /// <summary>Main thread: resolve everything the worker reads. False when git is unusable.</summary>
        static bool TryBegin(out Context ctx)
        {
            ctx = null;
            if (BugLedger.RootDir == null || !FrogletGit.IsAvailable) return false;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return false;

            try
            {
                _ = FrogletGit.TopLevel;
                var shared = FrogletGit.ToRepoRelative(Path.Combine(BugLedger.RootDir, "shared"));
                var library = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Library");
                var user = Environment.UserName;
                ctx = new Context
                {
                    User = user,
                    Branch = BranchFor(user),
                    RemoteRef = "refs/remotes/origin/" + BranchFor(user),
                    IssuesDir = shared + "/issues",
                    ReportsDir = shared + "/reports",
                    IndexPath = Path.Combine(library, "BugLedgerSend.index"),
                    TempDir = Path.Combine(library, "BugLedgerSend"),
                };
                ctx.ReportCandidates = BugHuntReports.Candidates(ctx.ReportsDir, user);
                return true;
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref _busy, 0);
                Debug.LogWarning($"[BugLedger] Send/sync not started: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        static void Start(string name, Action body)
        {
            new Thread(() =>
            {
                try { body(); }
                catch (Exception e) { Debug.LogWarning($"[BugLedger] {name} failed: {e.GetType().Name}: {e.Message}"); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            })
            { Name = name, IsBackground = true }.Start();
        }

        /// <summary>Returns null on success, else the reason (the caller retries once from a fresh fetch).</summary>
        static string SendOnce(Context ctx, int attempt)
        {
            var remote = Fetch(ctx);
            AdoptFixes(remote);

            // Issue files: this machine's live store, except where Claude's fix is newer than
            // anything this machine has adopted (its commit is not pulled here yet) — then the
            // branch keeps Claude's claim, refreshed with this machine's latest sighting.
            var files = new List<(string path, string json)>();
            int open = 0;
            foreach (var local in BugLedger.CopyLive())
            {
                if (local.IsOpen) open++;
                remote.Issues.TryGetValue(local.Id, out var published);
                var keep = BugLedger.IsNewerFix(published, local) ? published : local;
                if (keep == published)
                {
                    keep.TimesSeen = Math.Max(published.TimesSeen, local.TimesSeen);
                    keep.LastSeenUtc = local.LastSeenUtc;
                }
                files.Add(($"{ctx.IssuesDir}/{local.Id}.bug.json", keep.ToJson()));
            }

            var reports = new List<BugHuntReports.Report>();
            foreach (var r in ctx.ReportCandidates)
                if (!remote.ReportPaths.Contains(r.RepoPath)) reports.Add(r);
            reports = BugHuntReports.Cap(reports);

            var env = new[] { new KeyValuePair<string, string>("GIT_INDEX_FILE", ctx.IndexPath) };
            TryDelete(ctx.IndexPath);
            Directory.CreateDirectory(ctx.TempDir);

            var read = remote.Parent != null
                ? FrogletGit.RunWithEnv(env, "read-tree", remote.Parent)
                : FrogletGit.RunWithEnv(env, "read-tree", "--empty");
            if (!read.Ok) return "read-tree: " + read.Text.Trim();
            FrogletGit.RunWithEnv(env, "rm", "--cached", "-r", "-q", "--ignore-unmatch", "--", ctx.IssuesDir);

            int n = 0;
            foreach (var (path, json) in files)
            {
                var tmp = Path.Combine(ctx.TempDir, $"issue{n++}.json");
                File.WriteAllText(tmp, json);
                if (!Stage(env, tmp, path, out var why)) return why;
            }
            foreach (var r in reports)
                if (!Stage(env, r.Source, r.RepoPath, out var why)) return why;

            var tree = FrogletGit.RunWithEnv(env, "write-tree");
            if (!tree.Ok) return "write-tree: " + tree.Text.Trim();
            var treeSha = tree.StdOut.Trim();

            if (remote.Parent != null)
            {
                var parentTree = FrogletGit.Run("rev-parse", remote.Parent + "^{tree}");
                if (parentTree.Ok && parentTree.StdOut.Trim() == treeSha)
                {
                    Debug.Log("[BugLedger] Nothing new to send — Claude already has every error from this machine.");
                    return null;
                }
            }

            var message = $"bugledger: {open} open error(s) from {ctx.User}'s play tests" +
                          (reports.Count > 0 ? $", {reports.Count} report file(s)" : "");
            var commit = remote.Parent != null
                ? FrogletGit.Run("commit-tree", treeSha, "-p", remote.Parent, "-m", message)
                : FrogletGit.Run("commit-tree", treeSha, "-m", message);
            if (!commit.Ok) return "commit-tree: " + commit.Text.Trim();

            var push = FrogletGit.Run("push", "origin", commit.StdOut.Trim() + ":refs/heads/" + ctx.Branch);
            if (!push.Ok)
                return attempt == 0 ? "push rejected, retrying" : "push: " + push.Text.Trim();

            Debug.Log($"[BugLedger] Sent to Claude: {open} open error(s)" +
                      (reports.Count > 0 ? $" and {reports.Count} report file(s)" : "") +
                      $" on branch {ctx.Branch}. Ask Claude to fix them, or wait for the daily sweep.");
            TryDelete(ctx.IndexPath);
            return null;
        }

        static bool Stage(KeyValuePair<string, string>[] env, string file, string repoPath, out string why)
        {
            why = null;
            var hash = FrogletGit.Run("hash-object", "-w", file);
            if (!hash.Ok) { why = "hash-object: " + hash.Text.Trim(); return false; }
            var add = FrogletGit.RunWithEnv(env, "update-index", "--add", "--cacheinfo",
                "100644," + hash.StdOut.Trim() + "," + repoPath);
            if (!add.Ok) { why = "update-index: " + add.Text.Trim(); return false; }
            return true;
        }

        static RemoteBranch Fetch(Context ctx)
        {
            var remote = new RemoteBranch();
            // A missing branch fails the fetch: that is the first-send case, not an error.
            FrogletGit.Run("fetch", "-q", "origin", "+refs/heads/" + ctx.Branch + ":" + ctx.RemoteRef);
            var parent = FrogletGit.Run("rev-parse", "--verify", "-q", ctx.RemoteRef + "^{commit}");
            if (!parent.Ok || parent.StdOut.Trim().Length == 0) return remote;
            remote.Parent = parent.StdOut.Trim();

            var issues = FrogletGit.Run("ls-tree", "-r", "--name-only", remote.Parent, "--", ctx.IssuesDir);
            if (issues.Ok)
                foreach (var raw in issues.StdOut.Split('\n'))
                {
                    var path = raw.Trim();
                    if (!path.EndsWith(".bug.json", StringComparison.Ordinal)) continue;
                    var blob = FrogletGit.Run("cat-file", "blob", remote.Parent + ":" + path);
                    var issue = blob.Ok ? BugLedgerIssue.FromJson(blob.StdOut) : null;
                    if (issue != null) remote.Issues[issue.Id] = issue;
                }

            var reports = FrogletGit.Run("ls-tree", "-r", "--name-only", remote.Parent, "--", ctx.ReportsDir);
            if (reports.Ok)
                foreach (var raw in reports.StdOut.Split('\n'))
                {
                    var path = raw.Trim();
                    if (path.Length > 0) remote.ReportPaths.Add(path);
                }
            return remote;
        }

        static void AdoptFixes(RemoteBranch remote)
        {
            foreach (var published in remote.Issues.Values)
                BugLedger.AdoptPublishedFix(published,
                    sha => FrogletGit.Run("merge-base", "--is-ancestor", sha, "HEAD").Ok);
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* a stale index is reset by read-tree anyway */ }
        }
    }
}
