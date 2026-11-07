using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>One changed path in the working tree (<c>git status --porcelain=v1 -z</c>).</summary>
    public sealed class ScChange
    {
        /// <summary>Repo-relative, forward slashes. For a rename, the new path.</summary>
        public string Path;
        /// <summary>For a rename, the old path; otherwise null.</summary>
        public string OriginalPath;
        /// <summary>Two-character porcelain code: <c>" M"</c>, <c>"A "</c>, <c>"??"</c>, <c>"UU"</c>...</summary>
        public string Code;

        public bool IsUntracked => Code == "??";
        public bool IsDeleted => Code.IndexOf('D') >= 0 && !IsConflicted;
        public bool IsRenamed => Code.IndexOf('R') >= 0;
        public bool IsConflicted => Code == "UU" || Code == "AA" || Code == "DD" || Code.IndexOf('U') >= 0;

        /// <summary>One letter for the list, as GitHub Desktop shows it.</summary>
        public char Kind => IsConflicted ? '!' : IsUntracked ? '+' : IsDeleted ? '-' : IsRenamed ? '>' : Code.IndexOf('A') >= 0 ? '+' : '~';
    }

    /// <summary>A row in the Changes list: an asset and, when it changed too, its <c>.meta</c> — they are
    /// committed or discarded together, the way Unity treats them.</summary>
    public sealed class ScRow
    {
        public ScChange Change;
        public ScChange Meta;

        public string Path => Change.Path;

        /// <summary>Every path this row stands for.</summary>
        public IEnumerable<string> Paths()
        {
            yield return Change.Path;
            if (Change.OriginalPath != null) yield return Change.OriginalPath;
            if (Meta != null)
            {
                yield return Meta.Path;
                if (Meta.OriginalPath != null) yield return Meta.OriginalPath;
            }
        }
    }

    public sealed class ScBranch
    {
        /// <summary>Short name: <c>claude/x</c> for a local branch, <c>claude/x</c> (without origin/) for a remote one.</summary>
        public string Name;
        public bool IsRemote;
        public string Date;
        public string Subject;
    }

    public sealed class ScCommit
    {
        public string Sha;
        public string ShortSha;
        public string Author;
        public string Date;
        public string Subject;
        public bool Unpushed;
    }

    // The shape Tools/Build/push_checks.py --json prints.
    [Serializable] public sealed class ScCheckBehind { public int merges; public int commits; }

    [Serializable]
    public sealed class ScFinding
    {
        public string id;
        public string severity;
        public string title;
        public string detail;
        public string[] paths;
        public string fix;

        public bool IsBlock => severity == "block";
        public bool IsWarn => severity == "warn";
    }

    [Serializable]
    public sealed class ScCheckReport
    {
        public string branch;
        public string @base;
        public ScCheckBehind behind;
        public int @checked;
        public int unpushedPaths;
        public ScFinding[] findings;
    }

    /// <summary>
    /// The git work behind FrogletTools > Build > <b>Source Control</b>: parsers (pure, unit-tested in
    /// <c>FrogletSourceControlTests</c>) and the operations. Every operation blocks, so the window runs them on a
    /// worker thread; none of them touches a Unity API. Rules about WHAT may be pushed live in python
    /// (<c>Tools/Build/push_checks.py</c>, <c>Tools/Build/ship_gates.py</c>), not here.
    /// </summary>
    public static class FrogletSourceControl
    {
        public const string BaseBranch = "bleeding-edge";
        public const string BaseRef = "origin/" + BaseBranch;
        public const string PushChecksScript = "Tools/Build/push_checks.py";
        public const string ShipGatesScript = "Tools/Build/ship_gates.py";

        // ── Parsers ──────────────────────────────────────────────────────────────

        /// <summary><c>git status --porcelain=v1 -z --untracked-files=all</c> → changes. A rename's entry is
        /// followed by its source path as its own NUL-separated field.</summary>
        public static List<ScChange> ParseStatus(string z)
        {
            var list = new List<ScChange>();
            if (string.IsNullOrEmpty(z)) return list;
            var parts = z.Split('\0');
            for (var i = 0; i < parts.Length; i++)
            {
                var e = parts[i];
                if (e.Length < 4) continue;
                var c = new ScChange { Code = e.Substring(0, 2), Path = e.Substring(3).Replace('\\', '/') };
                if ((c.Code[0] == 'R' || c.Code[0] == 'C') && i + 1 < parts.Length)
                    c.OriginalPath = parts[++i].Replace('\\', '/');
                list.Add(c);
            }
            return list;
        }

        /// <summary>Folds each <c>X.meta</c> into its asset's row when both changed. A meta whose asset did not
        /// change keeps a row of its own (a GUID or importer change is worth seeing).</summary>
        public static List<ScRow> GroupWithMetas(List<ScChange> changes)
        {
            var byPath = new Dictionary<string, ScChange>(StringComparer.Ordinal);
            foreach (var c in changes) byPath[c.Path] = c;
            var rows = new List<ScRow>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in changes)
            {
                if (c.Path.EndsWith(".meta", StringComparison.Ordinal) &&
                    byPath.ContainsKey(c.Path.Substring(0, c.Path.Length - 5)))
                    continue; // folded into its asset's row below
                var row = new ScRow { Change = c };
                if (byPath.TryGetValue(c.Path + ".meta", out var meta)) { row.Meta = meta; used.Add(meta.Path); }
                rows.Add(row);
            }
            rows.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            return rows;
        }

        /// <summary><c>git rev-list --left-right --count A...B</c> → (left, right).</summary>
        public static bool TryParseCounts(string s, out int left, out int right)
        {
            left = right = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var parts = s.Trim().Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && int.TryParse(parts[0], out left) && int.TryParse(parts[1], out right);
        }

        /// <summary><c>git for-each-ref --format=%(refname)%09%(committerdate:short)%09%(subject)</c> → branches,
        /// in the order given. origin/HEAD is skipped.</summary>
        public static List<ScBranch> ParseBranches(string s)
        {
            var list = new List<ScBranch>();
            foreach (var raw in (s ?? string.Empty).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                var f = line.Split('\t');
                var r = f[0];
                ScBranch b;
                if (r.StartsWith("refs/heads/", StringComparison.Ordinal))
                    b = new ScBranch { Name = r.Substring(11) };
                else if (r.StartsWith("refs/remotes/origin/", StringComparison.Ordinal))
                    b = new ScBranch { Name = r.Substring(20), IsRemote = true };
                else continue;
                if (b.IsRemote && b.Name == "HEAD") continue;
                b.Date = f.Length > 1 ? f[1] : string.Empty;
                b.Subject = f.Length > 2 ? f[2] : string.Empty;
                list.Add(b);
            }
            return list;
        }

        const char Us = '\u001f';

        /// <summary><c>git log --format=%H%x1f%h%x1f%an%x1f%ad%x1f%s</c> → commits.</summary>
        public static List<ScCommit> ParseLog(string s, ICollection<string> unpushed)
        {
            var list = new List<ScCommit>();
            foreach (var raw in (s ?? string.Empty).Split('\n'))
            {
                var f = raw.TrimEnd('\r').Split(Us);
                if (f.Length < 5) continue;
                list.Add(new ScCommit
                {
                    Sha = f[0], ShortSha = f[1], Author = f[2], Date = f[3], Subject = f[4],
                    Unpushed = unpushed != null && unpushed.Contains(f[0]),
                });
            }
            return list;
        }

        /// <summary>The rules of <c>git check-ref-format --branch</c> that a person can trip over, checked
        /// before git is asked (git still has the last word).</summary>
        public static bool IsValidBranchName(string name, out string why)
        {
            why = null;
            if (string.IsNullOrWhiteSpace(name)) { why = "Type a name."; return false; }
            if (name.IndexOfAny(new[] { ' ', '~', '^', ':', '?', '*', '[', '\\', '\t' }) >= 0)
            { why = "No spaces or ~ ^ : ? * [ \\ in a branch name."; return false; }
            if (name.StartsWith("-") || name.StartsWith("/") || name.EndsWith("/") || name.EndsWith(".") ||
                name.EndsWith(".lock", StringComparison.Ordinal) || name.Contains("..") || name.Contains("//") ||
                name.Contains("@{") || name == "@")
            { why = "That is not a name git accepts (no leading - or /, no .., no // , no trailing / . or .lock)."; return false; }
            if (FrogletGit.IsProtectedBranch(name))
            { why = name + " is a protected branch; pick a name of your own (yourname/what-it-does)."; return false; }
            return true;
        }

        /// <summary>The GitHub "open a pull request" page for <paramref name="branch"/> into bleeding-edge, from
        /// the origin remote URL; null when origin is not a GitHub URL. GitHub fills the body from the repo's
        /// pull request template.</summary>
        public static string CompareUrl(string originUrl, string branch)
        {
            var root = FrogletDocLinks.ToWebRoot(originUrl?.Trim());
            if (root == null || string.IsNullOrEmpty(branch)) return null;
            return root + "/compare/" + BaseBranch + "..." + Uri.EscapeDataString(branch).Replace("%2F", "/") + "?expand=1";
        }

        /// <summary>The changed paths a commit made (<c>git show --name-status --format=</c>).</summary>
        public static List<string> ParseNameStatus(string s)
        {
            var list = new List<string>();
            foreach (var raw in (s ?? string.Empty).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length < 3) continue;
                list.Add(line.Replace('\t', ' '));
            }
            return list;
        }

        /// <summary>Paths to stage for a commit of these rows: every path the rows stand for, deduplicated.</summary>
        public static List<string> PathsOf(IEnumerable<ScRow> rows)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>();
            foreach (var r in rows)
                foreach (var p in r.Paths())
                    if (set.Add(p)) list.Add(p);
            return list;
        }

        // ── Queries (blocking; call on a worker thread) ─────────────────────────

        public static List<ScChange> Status()
            => ParseStatus(FrogletGit.Run("-c", "core.quotepath=false", "status", "--porcelain=v1", "-z",
                                          "--untracked-files=all").StdOut);

        public static string Upstream()
        {
            var r = FrogletGit.Run("rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
            return r.Ok ? r.StdOut.Trim() : null;
        }

        /// <summary>(commits on the upstream you do not have, commits you have that it does not).</summary>
        public static bool AheadBehind(string upstream, out int behind, out int ahead)
        {
            behind = ahead = 0;
            if (string.IsNullOrEmpty(upstream)) return false;
            var r = FrogletGit.Run("rev-list", "--left-right", "--count", upstream + "...HEAD");
            return r.Ok && TryParseCounts(r.StdOut, out behind, out ahead);
        }

        /// <summary>How many pull requests (merge commits) and commits bleeding-edge has that HEAD lacks.</summary>
        public static bool BehindBase(out int merges, out int commits)
        {
            merges = commits = 0;
            var m = FrogletGit.Run("rev-list", "--count", "--merges", "HEAD.." + BaseRef);
            var c = FrogletGit.Run("rev-list", "--count", "HEAD.." + BaseRef);
            return m.Ok && c.Ok && int.TryParse(m.StdOut.Trim(), out merges) && int.TryParse(c.StdOut.Trim(), out commits);
        }

        public static List<ScBranch> Branches()
            => ParseBranches(FrogletGit.Run("for-each-ref", "--sort=-committerdate",
                "--format=%(refname)%09%(committerdate:short)%09%(subject)", "refs/heads", "refs/remotes/origin").StdOut);

        public static List<ScCommit> History(int count = 100)
        {
            var unpushed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in FrogletGit.Run("rev-list", "HEAD", "--not", "--remotes=origin").StdOut.Split('\n'))
                if (l.Trim().Length > 0) unpushed.Add(l.Trim());
            var log = FrogletGit.Run("log", "-n", count.ToString(), "--date=short",
                                     "--format=%H%x1f%h%x1f%an%x1f%ad%x1f%s");
            return ParseLog(log.StdOut, unpushed);
        }

        public static List<string> CommitFiles(string sha)
            => ParseNameStatus(FrogletGit.Run("-c", "core.quotepath=false", "show", "--name-status", "--format=", sha).StdOut);

        /// <summary>A readable diff of one row, cut to <paramref name="maxLines"/>.</summary>
        public static string Diff(ScRow row, int maxLines = 600)
        {
            var c = row.Change;
            string text;
            if (c.IsUntracked)
                text = NewFilePreview(c.Path, maxLines);
            else
            {
                var args = new List<string> { "--literal-pathspecs", "-c", "core.quotepath=false", "diff", "--no-color", "HEAD", "--" };
                args.AddRange(row.Paths());
                text = FrogletGit.Run(args.ToArray()).StdOut;
                if (row.Meta != null && row.Meta.IsUntracked) text += "\n" + NewFilePreview(row.Meta.Path, 40);
            }
            if (string.IsNullOrEmpty(text)) return "(no text difference: a mode change or an empty file)";
            var lines = text.Split('\n');
            if (lines.Length <= maxLines) return text;
            return string.Join("\n", lines, 0, maxLines) + "\n\n… " + (lines.Length - maxLines) + " more lines (Show in folder to open the file)";
        }

        static string NewFilePreview(string path, int maxLines)
        {
            var full = FrogletGit.ToAbsolute(path);
            try
            {
                var info = new FileInfo(full);
                if (!info.Exists) return "(new: " + path + ")";
                using var fs = File.OpenRead(full);
                var buf = new byte[Math.Min(8000, (int)Math.Min(info.Length, int.MaxValue))];
                var n = fs.Read(buf, 0, buf.Length);
                for (var i = 0; i < n; i++)
                    if (buf[i] == 0) return "(new binary file, " + info.Length.ToString("N0") + " bytes: " + path + ")";
                var head = new StringBuilder("new file: " + path + " (" + info.Length.ToString("N0") + " bytes)\n");
                var lines = File.ReadLines(full);
                var k = 0;
                foreach (var l in lines)
                {
                    if (k++ >= maxLines) { head.Append("…\n"); break; }
                    head.Append('+').Append(l).Append('\n');
                }
                return head.ToString();
            }
            catch (Exception e) { return "(could not read " + path + ": " + e.Message + ")"; }
        }

        public static string OriginUrl()
        {
            var r = FrogletGit.Run("remote", "get-url", "origin");
            return r.Ok ? r.StdOut.Trim() : null;
        }

        // ── Operations (blocking; call on a worker thread) ──────────────────────

        public static FrogletGitResult Fetch()
            => FrogletGit.RunTimed(FrogletGit.NetworkTimeoutMs, "fetch", "--prune", "origin");

        /// <summary>Stage exactly these paths (additions, edits and deletions) and commit them, leaving anything
        /// else the person staged untouched. Paths go through a file, never the command line: a big commit's path
        /// list overruns Windows' 32k command-line limit.</summary>
        public static FrogletGitResult Commit(IList<string> paths, string summary, string description)
        {
            if (paths == null || paths.Count == 0)
                return new FrogletGitResult("git commit", 1, string.Empty, "Tick at least one file to commit.");
            var list = WriteTemp(string.Join("\n", paths) + "\n");
            var msg = WriteTemp(summary.Trim() + (string.IsNullOrWhiteSpace(description) ? "" : "\n\n" + description.Trim()) + "\n");
            try
            {
                var add = FrogletGit.Run("--literal-pathspecs", "add", "-A", "--pathspec-from-file=" + list);
                if (!add.Ok) return add;
                return FrogletGit.Run("--literal-pathspecs", "commit", "-F", msg, "--pathspec-from-file=" + list);
            }
            finally { TryDelete(list); TryDelete(msg); }
        }

        /// <summary>Throw away the working-tree changes of these rows: tracked files go back to HEAD, new files
        /// are deleted. Irreversible — the window confirms first.</summary>
        public static FrogletGitResult Discard(IEnumerable<ScRow> rows)
        {
            var tracked = new List<string>();
            var errors = new StringBuilder();
            foreach (var row in rows)
                foreach (var c in new[] { row.Change, row.Meta })
                {
                    if (c == null) continue;
                    if (c.IsUntracked)
                    {
                        try { File.Delete(FrogletGit.ToAbsolute(c.Path)); }
                        catch (Exception e) { errors.AppendLine(c.Path + ": " + e.Message); }
                    }
                    else
                    {
                        tracked.Add(c.Path);
                        if (c.OriginalPath != null) tracked.Add(c.OriginalPath);
                    }
                }
            if (tracked.Count > 0)
            {
                var list = WriteTemp(string.Join("\n", tracked) + "\n");
                try
                {
                    var r = FrogletGit.Run("--literal-pathspecs", "restore", "--source=HEAD", "--staged", "--worktree", "--pathspec-from-file=" + list);
                    if (!r.Ok) errors.AppendLine(r.Text);
                }
                finally { TryDelete(list); }
            }
            return new FrogletGitResult("discard", errors.Length == 0 ? 0 : 1, string.Empty, errors.ToString());
        }

        /// <summary>Discard specific repo-relative paths (a check's one-click fix).</summary>
        public static FrogletGitResult DiscardPaths(IEnumerable<string> paths, List<ScChange> status)
        {
            var want = new HashSet<string>(paths, StringComparer.Ordinal);
            var rows = new List<ScRow>();
            foreach (var c in status)
                if (want.Contains(c.Path)) rows.Add(new ScRow { Change = c });
            return Discard(rows);
        }

        public static FrogletGitResult Push(string branch, bool setUpstream)
            => setUpstream
                ? FrogletGit.RunTimed(FrogletGit.NetworkTimeoutMs, "push", "-u", "origin", branch)
                : FrogletGit.RunTimed(FrogletGit.NetworkTimeoutMs, "push", "origin", branch);

        /// <summary>Pull the branch's own upstream. A merge, never a rebase (other people push to shared
        /// branches all day); on a conflict the merge is undone and the files are named.</summary>
        public static FrogletGitResult Pull()
            => MergeOrAbort(() => FrogletGit.RunTimed(FrogletGit.NetworkTimeoutMs, "pull", "--no-rebase", "--no-edit"));

        /// <summary>Merge origin/bleeding-edge into the current branch (fetch first). Conflicts are undone and
        /// named, never left half-merged in the editor: resolving a prefab conflict needs care (/ship §1).</summary>
        public static FrogletGitResult UpdateFromBase()
            => MergeOrAbort(() => FrogletGit.RunTimed(FrogletGit.NetworkTimeoutMs, "merge", "--no-ff", "--no-edit", BaseRef));

        static FrogletGitResult MergeOrAbort(Func<FrogletGitResult> merge)
        {
            var r = merge();
            if (r.Ok) return r;
            var conflicted = FrogletGit.Run("diff", "--name-only", "--diff-filter=U").StdOut.Trim();
            if (conflicted.Length == 0) return r;
            FrogletGit.Run("merge", "--abort");
            return new FrogletGitResult(r.Command, 2, r.StdOut,
                "CONFLICTS (the merge was undone, nothing changed):\n" + conflicted);
        }

        /// <summary>Switch to a branch; a remote-only branch gets a local branch that tracks it.</summary>
        public static FrogletGitResult Switch(ScBranch b)
            => b.IsRemote
                ? FrogletGit.Run("switch", "--track", "origin/" + b.Name)
                : FrogletGit.Run("switch", b.Name);

        /// <summary>A new branch from the latest bleeding-edge (fetch first), switched to.</summary>
        public static FrogletGitResult CreateBranch(string name, bool fromBase)
        {
            var check = FrogletGit.Run("check-ref-format", "--branch", name);
            if (!check.Ok) return new FrogletGitResult(check.Command, 1, string.Empty, "git does not accept that branch name.");
            return fromBase
                ? FrogletGit.Run("switch", "--no-track", "-c", name, BaseRef)
                : FrogletGit.Run("switch", "-c", name);
        }

        // ── Python rules ─────────────────────────────────────────────────────────

        /// <summary>Run push_checks.py on these paths (the unpushed commits are always included). Null report on
        /// failure, with the reason in <paramref name="error"/>.</summary>
        public static ScCheckReport RunPushChecks(IList<string> paths, out string error)
        {
            error = null;
            var list = WriteTemp(string.Join("\n", paths ?? new string[0]) + "\n");
            try
            {
                var r = FrogletPython.RunScript(PushChecksScript, new[] { "--json", "--paths-from", list }, 5 * 60 * 1000);
                if (!r.Ok) { error = (r.StdErr + "\n" + r.StdOut).Trim(); return null; }
                var report = JsonUtility.FromJson<ScCheckReport>(r.StdOut);
                if (report == null) error = "push_checks.py printed no report.";
                else report.findings ??= new ScFinding[0];
                return report;
            }
            catch (Exception e) { error = e.Message; return null; }
            finally { TryDelete(list); }
        }

        static string WriteTemp(string content)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "froglet-sc-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (Exception) { /* temp file; the OS cleans it */ }
        }
    }
}
