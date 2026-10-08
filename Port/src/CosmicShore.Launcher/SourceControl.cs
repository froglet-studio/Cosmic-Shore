using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Git for Prisma's workspace, behind the GIT page. The agent's edits (and any made by hand)
    /// land in the workspace, which is not the user's own clone; this is how they get from there
    /// to GitHub: see every change and its diff, commit it to a branch of its own, push it, open a
    /// pull request, or throw it away. Nothing here runs on its own: every write is a button.
    /// </summary>
    public sealed class SourceControl
    {
        public sealed record Change(string Path, string Code, int Added, int Removed, bool Binary)
        {
            public bool Untracked => Code == "??";
            /// <summary>One letter, as GitHub Desktop and git show it.</summary>
            public char Letter => Untracked ? 'A' : Code.Trim().FirstOrDefault(c => c != ' ') switch { 'R' => 'R', 'D' => 'D', 'A' => 'A', _ => 'M' };
        }

        public sealed record LogEntry(string Sha, string Subject, string Author, string When, bool Unpushed);

        public sealed record State(string? Branch, string Head, string? Upstream, int Ahead, List<Change> Changes, List<LogEntry> Log, string? Error)
        {
            public bool Detached => Branch == null;
        }

        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly Workspace _ws;

        public SourceControl(LauncherSettings s, Toolchain tools, Workspace ws) { _s = s; _tools = tools; _ws = ws; }

        public State? Last { get; private set; }
        public DateTime LastAt { get; private set; }
        public bool Busy { get; private set; }

        string Git => _tools.Git ?? throw new InvalidOperationException("git was not found (press START once to set it up).");

        async Task<ProcessRunner.Result> G(LogBuffer? log, params string[] args) =>
            await ProcessRunner.Run(Git, new[] { "-C", _ws.Dir, "-c", "core.quotepath=off" }.Concat(args), null, log, CancellationToken.None, _ws.GitEnv(), quiet: log == null);

        /// <summary>Reads the workspace: where HEAD is, every change with its line counts, and the recent history.</summary>
        public async Task<State?> Refresh()
        {
            if (!_ws.Exists || _tools.Git == null) { Last = null; return null; }
            try
            {
                var branch = (await G(null, "symbolic-ref", "--short", "-q", "HEAD")).StdOut.Trim();
                var head = (await G(null, "rev-parse", "--short", "HEAD")).StdOut.Trim();
                var up = await G(null, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
                string? upstream = up.ExitCode == 0 ? up.StdOut.Trim() : null;

                var changes = new List<Change>();
                var counts = new Dictionary<string, (int a, int d, bool bin)>(StringComparer.Ordinal);
                foreach (var l in (await G(null, "diff", "HEAD", "--numstat")).StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = l.Split('\t');
                    if (p.Length < 3) continue;
                    bool bin = p[0] == "-";
                    counts[p[2].Contains(" => ") ? RenameTarget(p[2]) : p[2]] = (bin ? 0 : int.Parse(p[0]), bin ? 0 : int.Parse(p[1]), bin);
                }
                foreach (var l in (await G(null, "status", "--porcelain=v1", "-uall")).StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (l.Length < 4) continue;
                    string code = l[..2], path = l[3..].TrimEnd('\r');
                    if (path.Contains(" -> ")) path = path[(path.IndexOf(" -> ", StringComparison.Ordinal) + 4)..];
                    path = path.Trim('"');
                    if (code == "??")
                    {
                        var full = Path.Combine(_ws.Dir, path);
                        int lines = 0; bool bin = false;
                        try
                        {
                            var info = new FileInfo(full);
                            if (info.Length < 4 << 20) { var text = File.ReadAllText(full); bin = text.Contains('\0'); lines = bin ? 0 : text.Split('\n').Length; }
                            else bin = true;
                        }
                        catch { }
                        changes.Add(new Change(path, code, lines, 0, bin));
                    }
                    else
                    {
                        counts.TryGetValue(path, out var c);
                        changes.Add(new Change(path, code, c.a, c.d, c.bin));
                    }
                }

                var unpushed = new HashSet<string>((await G(null, "rev-list", "--abbrev-commit", "HEAD", "--not", "--remotes"))
                    .StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                var log = new List<LogEntry>();
                foreach (var l in (await G(null, "log", "-n", "15", "--format=%h%x1f%s%x1f%an%x1f%cr")).StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = l.Split('\x1f');
                    if (p.Length == 4) log.Add(new LogEntry(p[0], p[1], p[2], p[3], unpushed.Contains(p[0])));
                }
                int ahead = log.Count(x => x.Unpushed);
                Last = new State(branch.Length > 0 ? branch : null, head, upstream, ahead, changes.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList(), log, null);
            }
            catch (Exception e) { Last = new State(null, "", null, 0, new(), new(), e.Message); }
            LastAt = DateTime.Now;
            return Last;
        }

        static string RenameTarget(string numstatPath)
        {
            // "dir/{old => new}/f" or "old => new"
            var m = Regex.Match(numstatPath, @"^(.*)\{(.*) => (.*)\}(.*)$");
            return m.Success ? (m.Groups[1].Value + m.Groups[3].Value + m.Groups[4].Value).Replace("//", "/")
                : numstatPath[(numstatPath.IndexOf(" => ", StringComparison.Ordinal) + 4)..];
        }

        /// <summary>The unified diff of one change (a new file shows as all additions). Long diffs are cut.</summary>
        public async Task<List<string>> Diff(Change c)
        {
            if (c.Binary) return new() { "Binary file - no text diff." };
            List<string> lines;
            if (c.Untracked)
            {
                try { lines = File.ReadAllLines(Path.Combine(_ws.Dir, c.Path)).Select(l => "+" + l).Prepend($"@@ new file {c.Path} @@").ToList(); }
                catch (Exception e) { lines = new() { e.Message }; }
            }
            else
                lines = (await G(null, "diff", "HEAD", "--", c.Path)).StdOut.Replace("\r", "").Split('\n')
                    .SkipWhile(l => !l.StartsWith("@@")).ToList();
            return lines.Count > 4000 ? lines.Take(4000).Append($"... {lines.Count - 4000} more lines").ToList() : lines;
        }

        /// <summary>A branch name git accepts, from free text.</summary>
        public static string Slug(string text)
        {
            var s = Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (s.Length > 40) s = s[..40].TrimEnd('-');
            return s.Length == 0 ? DateTime.Now.ToString("yyyyMMdd-HHmm") : s;
        }

        public static bool ValidBranch(string b) =>
            b.Length > 0 && !b.StartsWith('-') && !b.EndsWith('/') && !b.EndsWith(".lock") && !b.Contains("..") &&
            !Regex.IsMatch(b, @"[\s~^:?*\[\\]|@\{|//");

        async Task<string?> Run(string what, Func<LogBuffer, Task<string?>> body, LogBuffer log)
        {
            if (Busy) return "Busy with another git step.";
            Busy = true;
            try { return await body(log); }
            catch (Exception e) { return what + ": " + e.Message; }
            finally { Busy = false; await Refresh(); }
        }

        static string Why(ProcessRunner.Result r)
        {
            var lines = (r.StdErr + "\n" + r.StdOut).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("hint:")).ToList();
            return lines.FirstOrDefault(l => l.StartsWith("fatal") || l.StartsWith("error") || l.StartsWith("!")) ?? lines.FirstOrDefault() ?? $"git exited with {r.ExitCode}";
        }

        /// <summary>
        /// Commits every change to <paramref name="branch"/>: from a detached HEAD (where START leaves
        /// the workspace) the branch is created here, so the commit has a name to push. Returns an error, or null.
        /// </summary>
        public Task<string?> Commit(string branch, string message, string name, string email, LogBuffer log) => Run("Commit", async l =>
        {
            branch = branch.Trim();
            if (!ValidBranch(branch)) return $"'{branch}' is not a branch name git accepts.";
            if (string.IsNullOrWhiteSpace(message)) return "Write a commit message first.";
            var st = Last ?? await Refresh();
            if (st == null) return "No workspace.";
            if (st.Branch != branch)
            {
                bool exists = (await G(null, "rev-parse", "--verify", "--quiet", "refs/heads/" + branch)).ExitCode == 0;
                var sw = exists ? await G(l, "switch", branch) : await G(l, "switch", "-c", branch);
                if (sw.ExitCode != 0) return (exists ? $"Could not switch to the existing branch {branch} (it may not contain these files' base): " : "Could not create the branch: ") + Why(sw);
            }
            var add = await G(l, "add", "-A");
            if (add.ExitCode != 0) return "git add failed: " + Why(add);
            var args = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) { args.Add("-c"); args.Add("user.name=" + name.Trim()); }
            if (!string.IsNullOrWhiteSpace(email)) { args.Add("-c"); args.Add("user.email=" + email.Trim()); }
            args.AddRange(new[] { "commit", "-q", "-m", message.Trim() });
            var c = await G(l, args.ToArray());
            if (c.ExitCode == 0) return null;
            var why = Why(c);
            return why.Contains("tell me who you are", StringComparison.OrdinalIgnoreCase) || (c.StdErr + c.StdOut).Contains("user.email")
                ? "git does not know who you are: fill in your name and email under AUTHOR." : "Commit failed: " + why;
        }, log);

        /// <summary>Pushes the current branch to GitHub and sets it as its upstream.</summary>
        public Task<string?> Push(LogBuffer log) => Run("Push", async l =>
        {
            var st = Last ?? await Refresh();
            if (st?.Branch == null) return "Commit to a branch first: a detached workspace has nothing to push.";
            var r = await G(l, "push", "-u", "origin", "HEAD:refs/heads/" + st.Branch);
            if (r.ExitCode == 0) return null;
            var why = Why(r);
            return why.Contains("403") || why.Contains("Authentication") || why.Contains("could not read Username") || why.Contains("denied")
                ? "GitHub refused the push. Paste a GitHub token with Contents: read & write in SETTINGS > SOURCE, or push from GitHub Desktop." : "Push failed: " + why;
        }, log);

        /// <summary>Puts one file back as HEAD has it (a new file is deleted).</summary>
        public Task<string?> Revert(Change c, LogBuffer log) => Run("Revert", async l =>
        {
            if (c.Untracked) { File.Delete(Path.Combine(_ws.Dir, c.Path)); return null; }
            var r = await G(l, "restore", "--source=HEAD", "--staged", "--worktree", "--", c.Path);
            return r.ExitCode == 0 ? null : "Could not revert: " + Why(r);
        }, log);

        /// <summary>Throws every uncommitted change away (build output, which git ignores, stays).</summary>
        public Task<string?> DiscardAll(LogBuffer log) => Run("Discard", async l =>
        {
            var r = await G(l, "reset", "--hard", "-q");
            if (r.ExitCode != 0) return "Could not discard: " + Why(r);
            await G(l, "clean", "-fdq");
            return null;
        }, log);

        /// <summary>https://github.com/OWNER/REPO for the workspace's remote, or null when it is not GitHub.</summary>
        public string? WebUrl
        {
            get
            {
                var m = Regex.Match(_s.RemoteUrl, @"github\.com[/:]([^/]+)/([^/]+?)(\.git)?/?$");
                return m.Success ? $"https://github.com/{m.Groups[1].Value}/{m.Groups[2].Value}" : null;
            }
        }

        public string? PullRequestUrl(string branch, string into) => WebUrl is { } w ? $"{w}/compare/{Uri.EscapeDataString(into)}...{Uri.EscapeDataString(branch)}?expand=1" : null;
        public string? CommitUrl(string sha) => WebUrl is { } w ? $"{w}/commit/{sha}" : null;

        public static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        /// <summary>The name and email git already uses on this PC (GitHub Desktop sets them), for the AUTHOR fields.</summary>
        public (string name, string email) Identity()
        {
            if (_tools.Git == null) return ("", "");
            return (ProcessRunner.Capture(_tools.Git, "-C", _ws.Exists ? _ws.Dir : ".", "config", "user.name") ?? "",
                    ProcessRunner.Capture(_tools.Git, "-C", _ws.Exists ? _ws.Dir : ".", "config", "user.email") ?? "");
        }
    }
}
