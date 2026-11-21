using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amoebius;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// SEND TO CLAUDE after an online test: pushes the game's multiplayer test logs (<see cref="NetTestLogFormat"/>,
    /// written by each player at close into <see cref="LauncherJobs.NetLogsDir"/>) to <c>bugledger/&lt;user&gt;</c>
    /// under <c>BugLedger/shared/reports/&lt;user&gt;/net/</c> - the branch and folder the Unity editor's Send to
    /// Claude uses, so Claude reads both with one tool (<c>Tools/Build/net_logs.py</c>).
    ///
    /// <para>The commit is built in a throwaway index from blobs written straight into the object store, on top of
    /// the branch's current tip, and pushed: the workspace's checkout, its staged changes and its branch never move,
    /// and the branch's other files (the Unity editor's issues and reports) are kept as they are. Logs already on
    /// the branch are not sent again.</para>
    /// </summary>
    public static class NetLogSender
    {
        /// <summary>The newest logs one send carries (the Unity sender's per-send cap).</summary>
        public const int MaxPerSend = 12;
        static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

        public sealed record Result(int Sent, string Branch, string? Error)
        {
            public bool Ok => Error == null;
        }

        /// <summary>The logs in <paramref name="dir"/> young and small enough to send, newest first, at most <see cref="MaxPerSend"/>.</summary>
        public static List<FileInfo> Candidates(string dir) =>
            NetTestLogFormat.NewFiles(dir, new HashSet<string>())
                .Where(f => DateTime.UtcNow - f.LastWriteTimeUtc <= MaxAge)
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .Take(MaxPerSend).ToList();

        public static async Task<Result> Send(string git, string repoDir, IReadOnlyList<FileInfo> files, string user, LogBuffer? log, CancellationToken ct)
        {
            string branch = NetTestLogFormat.Branch(user);
            string? why = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var (sent, error) = await SendOnce(git, repoDir, files, user, branch, ct);
                if (error == null)
                {
                    log?.Add(LogKind.Success, sent == 0
                        ? "Claude already has every multiplayer test log from this PC."
                        : $"Sent {sent} multiplayer test log(s) to Claude on {branch}. Ask Claude to read them (Tools/Build/net_logs.py).");
                    return new Result(sent, branch, null);
                }
                why = error; // a push that lost a race retries once from a fresh fetch
            }
            log?.Add(LogKind.Error, "Could not send the multiplayer test logs: " + why);
            return new Result(0, branch, why);
        }

        static async Task<(int sent, string? error)> SendOnce(string git, string repoDir, IReadOnlyList<FileInfo> files, string user,
                                                              string branch, CancellationToken ct)
        {
            string remoteRef = "refs/remotes/origin/" + branch;
            // A missing branch fails the fetch: that is the first-send case, not an error.
            await Git(git, repoDir, ct, null, "fetch", "-q", "origin", "+refs/heads/" + branch + ":" + remoteRef);
            var parentRun = await Git(git, repoDir, ct, null, "rev-parse", "--verify", "-q", remoteRef + "^{commit}");
            string? parent = parentRun.ExitCode == 0 && parentRun.StdOut.Trim().Length > 0 ? parentRun.StdOut.Trim() : null;

            var onBranch = new HashSet<string>(StringComparer.Ordinal);
            if (parent != null)
            {
                var ls = await Git(git, repoDir, ct, null, "ls-tree", "-r", "--name-only", parent, "--",
                    Path.GetDirectoryName(NetTestLogFormat.ReportPath(user, "x"))!.Replace('\\', '/'));
                foreach (var line in ls.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)) onBranch.Add(line.Trim());
            }
            var toSend = files.Where(f => !onBranch.Contains(NetTestLogFormat.ReportPath(user, f.Name))).ToList();
            if (toSend.Count == 0) return (0, null);

            string index = Path.Combine(Path.GetTempPath(), $"amoebius-netlogs-{Guid.NewGuid():N}.index");
            var env = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = index };
            try
            {
                var read = parent != null
                    ? await Git(git, repoDir, ct, env, "read-tree", parent)
                    : await Git(git, repoDir, ct, env, "read-tree", "--empty");
                if (read.ExitCode != 0) return (0, "read-tree: " + read.StdErr.Trim());
                foreach (var f in toSend)
                {
                    var hash = await Git(git, repoDir, ct, null, "hash-object", "-w", f.FullName);
                    if (hash.ExitCode != 0) return (0, "hash-object: " + hash.StdErr.Trim());
                    var add = await Git(git, repoDir, ct, env, "update-index", "--add", "--cacheinfo",
                        "100644," + hash.StdOut.Trim() + "," + NetTestLogFormat.ReportPath(user, f.Name));
                    if (add.ExitCode != 0) return (0, "update-index: " + add.StdErr.Trim());
                }
                var tree = await Git(git, repoDir, ct, env, "write-tree");
                if (tree.ExitCode != 0) return (0, "write-tree: " + tree.StdErr.Trim());

                var peers = NetTestLogFormat.PeersOf(toSend.Select(f => f.Name));
                string message = $"bugledger: {toSend.Count} multiplayer test log(s) from {user}'s Amoebius online test ({NetTestLogFormat.Players(peers)})";
                var args = new List<string> { "commit-tree", tree.StdOut.Trim() };
                if (parent != null) { args.Add("-p"); args.Add(parent); }
                args.Add("-m"); args.Add(message);
                var commit = await Git(git, repoDir, ct, await Identity(git, repoDir, ct), args.ToArray());
                if (commit.ExitCode != 0) return (0, "commit-tree: " + commit.StdErr.Trim());

                var push = await Git(git, repoDir, ct, null, "push", "-q", "origin", commit.StdOut.Trim() + ":refs/heads/" + branch);
                if (push.ExitCode != 0) return (0, "push: " + push.StdErr.Trim());
                return (toSend.Count, null);
            }
            finally
            {
                try { File.Delete(index); } catch { /* a temp file */ }
            }
        }

        /// <summary>The workspace's own git identity signs the commit; a PC with none gets a neutral one rather than a failed send.</summary>
        static async Task<Dictionary<string, string>?> Identity(string git, string repoDir, CancellationToken ct)
        {
            var ident = await Git(git, repoDir, ct, null, "var", "GIT_COMMITTER_IDENT");
            if (ident.ExitCode == 0) return null;
            return new Dictionary<string, string>
            {
                ["GIT_AUTHOR_NAME"] = Environment.UserName, ["GIT_AUTHOR_EMAIL"] = "amoebius@localhost",
                ["GIT_COMMITTER_NAME"] = Environment.UserName, ["GIT_COMMITTER_EMAIL"] = "amoebius@localhost",
            };
        }

        static Task<ProcessRunner.Result> Git(string git, string repoDir, CancellationToken ct, IDictionary<string, string>? env, params string[] args) =>
            ProcessRunner.Run(git, args, repoDir, null, ct, env, quiet: true, closeStdin: true);
    }
}
