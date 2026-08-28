using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Editor.Froglet;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Bug Ledger's own, strictly-scoped git client — the engine behind the Stage &amp; Push
    /// tab (<see cref="BugLedgerStageView"/>).
    ///
    /// <para><b>Scope is the whole contract:</b> every git mutation here is limited to the
    /// ledger's own files — the staged issues under <c>BugLedger/shared/</c> plus the store's
    /// README and .gitignore. <c>git add</c> takes those explicit paths (never <c>-A</c>, never a
    /// wildcard — <see cref="FrogletGit"/> has no such path by design) and the commit carries the
    /// same pathspec, so anything else sitting in the working tree — staged or not — is never
    /// swept up. Nothing in your project can be committed by this tool except bug data.</para>
    ///
    /// <para><b>Threading:</b> one operation at a time, run on a background thread so the editor
    /// never blocks on the network; the view polls <see cref="Current"/> for the step + progress
    /// bar. <see cref="FrogletGit"/> is Process-based and safe off-thread ONCE its cached statics
    /// are resolved — <see cref="Warm"/> touches them on the main thread before every start.</para>
    ///
    /// <para><b>Branch policy:</b> pushes go to the CURRENT branch after an explicit confirm that
    /// names it. Unlike the ship panel, protected branches are allowed — deliberately: this is
    /// team DATA, not tool output, and "file a bug the whole team sees" must not require a feature
    /// branch and a PR. A publish refuses to run while the remote is ahead (the push would be
    /// rejected anyway) and asks for a pull instead — it never pulls or rebases your repo itself.</para>
    /// </summary>
    public static class BugLedgerPublisher
    {
        /// <summary>Live state of the running (or last finished) operation. Written by the worker
        /// thread, polled by the view — fields are volatile, lists are swapped by reference.</summary>
        public sealed class Operation
        {
            public readonly string Title;
            public volatile bool Running = true;
            public volatile bool Failed;
            public volatile float Progress;
            public volatile string Step = "starting…";
            public volatile string Result = "";

            public Operation(string title) { Title = title; }
        }

        public static Operation Current { get; private set; }
        public static bool IsBusy => Current is { Running: true };

        /// <summary>Commits ahead of / behind origin for the current branch (−1 = not fetched yet).</summary>
        public static int Ahead { get; private set; } = -1;
        public static int Behind { get; private set; } = -1;

        /// <summary>Ledger files origin has that HEAD does not, from the last fetch — display only.</summary>
        public static IReadOnlyList<string> IncomingChanges => _incoming;
        static List<string> _incoming = new();

        static string _branch;

        public static bool GitAvailable => FrogletGit.IsAvailable;

        /// <summary>Current branch, cached per warm. Empty/HEAD = nothing to push to.</summary>
        public static string Branch => _branch ?? "";

        public static bool CanPush
            => GitAvailable && !string.IsNullOrEmpty(Branch)
               && !string.Equals(Branch, "HEAD", StringComparison.OrdinalIgnoreCase);

        /// <summary>Main-thread-only: resolves every cached static the worker will read
        /// (git availability, repo roots, current branch).</summary>
        public static void Warm()
        {
            if (!FrogletGit.IsAvailable) { _branch = ""; return; }
            _ = FrogletGit.TopLevel;
            _ = FrogletGit.ProjectPrefix;
            _branch = FrogletGit.CurrentBranch();
        }

        // ── Operations ───────────────────────────────────────────────────────────

        /// <summary>Fetch + ahead/behind + the list of incoming ledger files. Read-only on the repo.</summary>
        public static void StartFetch()
        {
            if (IsBusy) return;
            Warm();
            if (!GitAvailable) return;
            var op = new Operation("Fetch");
            Current = op;
            StartThread("BugLedger.Fetch", () => FetchOp(op));
        }

        /// <summary>Applies the staged ids into shared/, then add → commit → push, all scoped to
        /// the ledger's paths. The view confirms (naming the branch) before calling this.</summary>
        public static void StartPublish(List<string> stagedIds, string message)
        {
            if (IsBusy || stagedIds == null || stagedIds.Count == 0) return;
            Warm();
            if (!CanPush) return;
            if (string.IsNullOrWhiteSpace(message)) message = "bugledger: update";
            var op = new Operation("Commit & Push");
            Current = op;
            var branch = Branch;
            StartThread("BugLedger.Publish", () => PublishOp(op, branch, stagedIds, message.Trim()));
        }

        static void StartThread(string name, ThreadStart body)
        {
            new Thread(() =>
            {
                try { body(); }
                catch (Exception e)
                {
                    var op = Current;
                    if (op != null)
                    {
                        op.Failed = true;
                        op.Result = $"{op.Step} — {e.GetType().Name}: {e.Message}";
                        op.Running = false;
                    }
                }
                finally { BugLedger.NotifyExternalChange(); }
            })
            { Name = name, IsBackground = true }.Start();
        }

        // ── Worker bodies (no Unity APIs beyond thread-safe Debug) ───────────────

        static void FetchOp(Operation op)
        {
            op.Step = "Fetching origin…";
            op.Progress = 0.25f;
            var fetch = FrogletGit.Run("fetch", "origin");
            if (!fetch.Ok)
            {
                Fail(op, "Fetch failed", fetch.Text);
                return;
            }

            op.Step = $"Comparing with origin/{_branch}…";
            op.Progress = 0.6f;
            ReadAheadBehind(_branch);

            op.Step = "Listing incoming ledger changes…";
            op.Progress = 0.85f;
            _incoming = ReadIncomingLedgerChanges(_branch);

            op.Progress = 1f;
            op.Result = Behind switch
            {
                < 0 => "Fetched. (No upstream to compare against.)",
                0 when Ahead <= 0 => $"Up to date with origin/{_branch}.",
                0 => $"Ahead of origin/{_branch} by {Ahead} commit(s) — ready to push.",
                _ => $"origin/{_branch} is ahead by {Behind} commit(s) — pull in your git client before pushing."
                     + (_incoming.Count > 0 ? $" {_incoming.Count} incoming ledger change(s)." : ""),
            };
            op.Running = false;
        }

        static void PublishOp(Operation op, string branch, List<string> stagedIds, string message)
        {
            op.Step = "Fetching origin…";
            op.Progress = 0.1f;
            var fetch = FrogletGit.Run("fetch", "origin");

            if (fetch.Ok)
            {
                op.Step = "Checking branch state…";
                op.Progress = 0.2f;
                ReadAheadBehind(branch);
                if (Behind > 0)
                {
                    // Nothing has been applied or committed yet — failing here is completely safe.
                    Fail(op, "Not pushed",
                        $"origin/{branch} is ahead by {Behind} commit(s); the push would be rejected. " +
                        "Pull in your git client, then stage & push again. Nothing was committed.");
                    return;
                }
            }

            op.Step = $"Publishing {stagedIds.Count} staged change(s)…";
            op.Progress = 0.35f;
            var absolute = BugLedger.ApplyStagedChanges(stagedIds);
            var paths = new List<string>(absolute.Count + 2);
            foreach (var abs in absolute)
            {
                var rel = FrogletGit.ToRepoRelative(abs);
                if (rel != null) paths.Add(rel);
            }
            // The store stays self-describing on its first ever push.
            AddIfExists(paths, System.IO.Path.Combine(BugLedger.RootDir, "README.md"));
            AddIfExists(paths, System.IO.Path.Combine(BugLedger.RootDir, ".gitignore"));

            op.Step = "Staging files with git…";
            op.Progress = 0.5f;
            var add = FrogletGit.Add(paths);
            if (!add.Ok)
            {
                Fail(op, "git add failed", add.Text);
                return;
            }

            op.Step = "Committing…";
            op.Progress = 0.65f;
            var commit = FrogletGit.Commit(message, paths);
            if (!commit.Ok && !FrogletGit.NothingToCommit(commit))
            {
                Fail(op, "git commit failed", commit.Text);
                return;
            }
            bool committed = commit.Ok;

            op.Step = $"Pushing to origin/{branch}…";
            op.Progress = 0.85f;
            var push = FrogletGit.PushWithRetry(branch);
            if (!push.Ok)
            {
                Fail(op, "git push failed", push.Text +
                    (committed ? "\n(The commit exists locally — fix the push issue and push from your git client, or press Commit & Push again.)" : ""));
                return;
            }

            op.Progress = 1f;
            op.Result = committed
                ? $"Pushed {stagedIds.Count} ledger change(s) to origin/{branch}."
                : $"Nothing new to commit — origin/{branch} already has these files. Pushed.";
            op.Running = false;
            Debug.Log($"[BugLedger] {op.Result}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static void Fail(Operation op, string headline, string detail)
        {
            op.Failed = true;
            detail = detail?.Trim() ?? "";
            if (detail.Length > 600) detail = detail[..600] + "…";
            op.Result = detail.Length > 0 ? $"{headline}: {detail}" : headline;
            op.Progress = 1f;
            op.Running = false;
        }

        static void ReadAheadBehind(string branch)
        {
            Ahead = Behind = -1;
            if (string.IsNullOrEmpty(branch)) return;
            var r = FrogletGit.Run("rev-list", "--left-right", "--count", $"HEAD...origin/{branch}");
            if (!r.Ok) return;
            var parts = r.StdOut.Trim().Split('\t', ' ');
            if (parts.Length >= 2
                && int.TryParse(parts[0], out var ahead)
                && int.TryParse(parts[^1], out var behind))
            {
                Ahead = ahead;
                Behind = behind;
            }
        }

        static List<string> ReadIncomingLedgerChanges(string branch)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(branch)) return list;
            var scope = FrogletGit.ToRepoRelative(BugLedger.RootDir);
            if (scope == null) return list;
            var r = FrogletGit.Run("diff", "--name-status", $"HEAD..origin/{branch}", "--", scope);
            if (!r.Ok) return list;
            foreach (var raw in r.StdOut.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                list.Add(line.Replace('\t', ' '));
            }
            return list;
        }

        static void AddIfExists(List<string> paths, string absolutePath)
        {
            if (!System.IO.File.Exists(absolutePath)) return;
            var rel = FrogletGit.ToRepoRelative(absolutePath);
            if (rel != null && !paths.Contains(rel)) paths.Add(rel);
        }
    }
}
