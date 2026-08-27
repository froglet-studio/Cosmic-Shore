using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>Outcome of one <c>git</c> invocation.</summary>
    public readonly struct FrogletGitResult
    {
        /// <summary>The command line that produced this, for logging.</summary>
        public readonly string Command;

        public readonly int ExitCode;
        public readonly string StdOut;
        public readonly string StdErr;

        public FrogletGitResult(string command, int exitCode, string stdOut, string stdErr)
        {
            Command = command;
            ExitCode = exitCode;
            StdOut = stdOut ?? string.Empty;
            StdErr = stdErr ?? string.Empty;
        }

        public bool Ok => ExitCode == 0;

        /// <summary>Whichever stream carried the useful text. git writes plenty of normal
        /// progress to stderr, so neither stream alone is "the output".</summary>
        public string Text
        {
            get
            {
                var o = StdOut.Trim();
                var e = StdErr.Trim();
                if (o.Length == 0) return e;
                if (e.Length == 0) return o;
                return o + "\n" + e;
            }
        }
    }

    /// <summary>One line of <c>git status --porcelain</c>.</summary>
    public readonly struct FrogletGitChange
    {
        /// <summary>Two-character porcelain code, e.g. <c>" M"</c>, <c>"??"</c>, <c>"A "</c>, <c>" D"</c>.</summary>
        public readonly string Code;

        /// <summary>Repo-relative path, forward slashes. For a rename, the destination path.</summary>
        public readonly string Path;

        public FrogletGitChange(string code, string path)
        {
            Code = code ?? "  ";
            Path = path ?? string.Empty;
        }

        public bool IsUntracked => Code == "??";
        public bool IsIgnored => Code == "!!";
        public bool IsDeleted => Code.Length == 2 && (Code[0] == 'D' || Code[1] == 'D');

        public override string ToString() => Code + " " + Path;
    }

    /// <summary>
    /// A thin, quoting-safe wrapper around the <c>git</c> CLI for editor tools that need to
    /// commit their own output.
    ///
    /// Two rules this exists to enforce:
    /// <list type="bullet">
    /// <item>Every argument is quoted. Unity asset paths are full of spaces
    /// ("Assets/_SO_Assets/Cell Configs/..."), and an unquoted path is exactly how a multi-file
    /// git call silently operates on the wrong files.</item>
    /// <item>Nothing here ever stages by wildcard. Callers pass explicit paths, so a tool can
    /// only ever commit its OWN output and never sweeps up a half-finished edit sitting next
    /// to it in the working tree.</item>
    /// </list>
    /// </summary>
    public static class FrogletGit
    {
        const int TimeoutMs = 120000;

        static string _repoRoot;
        static string _topLevel;
        static string _projectPrefix;
        static int _available = -1; // -1 unknown, 0 no, 1 yes

        /// <summary>The folder that contains <c>Assets/</c>. Also the working directory git runs in.</summary>
        public static string RepoRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(_repoRoot)) return _repoRoot;
                var parent = Directory.GetParent(Application.dataPath);
                _repoRoot = parent != null ? parent.FullName : Application.dataPath;
                return _repoRoot;
            }
        }

        /// <summary>
        /// The git working tree root, which is NOT always the Unity project folder — a project
        /// can live in a subdirectory of its repo. Everything git reports is relative to this.
        /// </summary>
        public static string TopLevel
        {
            get
            {
                if (string.IsNullOrEmpty(_topLevel)) ResolveTopLevel();
                return _topLevel;
            }
        }

        /// <summary>
        /// Path from the git root to the Unity project, with a trailing slash — empty when they
        /// are the same folder. Prepended to project-relative paths ("Assets/Foo.prefab") to get
        /// the repo-relative path git speaks in.
        /// </summary>
        public static string ProjectPrefix
        {
            get
            {
                if (_projectPrefix == null) ResolveTopLevel();
                return _projectPrefix;
            }
        }

        static void ResolveTopLevel()
        {
            _topLevel = RepoRoot;
            _projectPrefix = string.Empty;

            var r = Run("rev-parse", "--show-toplevel");
            if (!r.Ok) return;

            var top = r.StdOut.Trim().Replace('\\', '/').TrimEnd('/');
            if (top.Length == 0) return;

            _topLevel = top;

            var project = RepoRoot.Replace('\\', '/').TrimEnd('/');
            if (project.Length > top.Length
                && project.StartsWith(top + "/", StringComparison.OrdinalIgnoreCase))
                _projectPrefix = project.Substring(top.Length + 1) + "/";
        }

        /// <summary>Absolute path of a repo-relative path git gave us.</summary>
        public static string ToAbsolute(string repoRelativePath)
            => Path.Combine(TopLevel, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>
        /// The single normalizer: turns an absolute path, a project-relative path
        /// ("Assets/Foo.prefab", which is what <c>AssetDatabase</c> speaks) or an already
        /// repo-relative path into the repo-relative form git speaks. Null when it is not a path
        /// at all. Everything that compares a recorded path against `git status` output goes
        /// through here — two conventions silently failing to match is how a "clean" tool ends up
        /// with uncommitted output.
        /// </summary>
        public static string ToRepoRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var p = path.Replace('\\', '/').Trim().TrimEnd('/');
            if (p.Length == 0) return null;

            var top = TopLevel.Replace('\\', '/').TrimEnd('/');
            var project = RepoRoot.Replace('\\', '/').TrimEnd('/');

            if (p.StartsWith(project + "/", StringComparison.OrdinalIgnoreCase))
                p = ProjectPrefix + p.Substring(project.Length + 1);
            else if (p.StartsWith(top + "/", StringComparison.OrdinalIgnoreCase))
                p = p.Substring(top.Length + 1);

            p = p.TrimStart('/');

            var prefix = ProjectPrefix;
            if (prefix.Length > 0
                && !p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)))
                p = prefix + p;

            return p.Length == 0 ? null : p;
        }

        /// <summary>Is a usable <c>git</c> on PATH? Cached — this shells out on first read.</summary>
        public static bool IsAvailable
        {
            get
            {
                if (_available < 0)
                {
                    try { _available = Run("--version").Ok ? 1 : 0; }
                    catch (Exception) { _available = 0; }
                }
                return _available == 1;
            }
        }

        /// <summary>Branches a tool refuses to push to. Tool output belongs on a feature branch.</summary>
        public static readonly string[] ProtectedBranches = { "main", "master", "bleeding-edge", "develop", "HEAD" };

        // ── Invocation ───────────────────────────────────────────────────────────

        public static FrogletGitResult Run(params string[] args) => RunIn(RepoRoot, args);

        /// <summary><see cref="Run"/> with environment variables. See <see cref="RunInWithEnv"/>.</summary>
        public static FrogletGitResult RunWithEnv(IEnumerable<KeyValuePair<string, string>> env,
                                                  params string[] args)
            => RunInWithEnv(RepoRoot, env, args);

        public static FrogletGitResult RunIn(string workingDirectory, params string[] args)
            => RunInWithEnv(workingDirectory, null, args);

        /// <summary>
        /// <see cref="RunIn"/> plus environment variables — the only way to reach the
        /// handful of git behaviours that have no command-line equivalent, chiefly
        /// <c>GIT_INDEX_FILE</c>. Staging into a THROWAWAY index is what lets a tool
        /// build a commit out of specific paths without disturbing the human's real
        /// index, their branch, or their working tree.
        /// </summary>
        public static FrogletGitResult RunInWithEnv(string workingDirectory,
                                                    IEnumerable<KeyValuePair<string, string>> env,
                                                    params string[] args)
        {
            var argString = BuildArguments(args);
            var display = "git " + argString;

            var psi = new ProcessStartInfo("git", argString)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (env != null)
                foreach (var kv in env)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;

            using (var p = new Process())
            {
                p.StartInfo = psi;

                var so = new StringBuilder();
                var se = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) se.AppendLine(e.Data); };

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                if (!p.WaitForExit(TimeoutMs))
                {
                    try { p.Kill(); }
                    catch (Exception) { /* already gone */ }
                    return new FrogletGitResult(display, -1, so.ToString(),
                        se + "\ngit timed out after " + (TimeoutMs / 1000) + "s.");
                }

                p.WaitForExit(); // second, argument-less call flushes the async readers
                return new FrogletGitResult(display, p.ExitCode, so.ToString(), se.ToString());
            }
        }

        // ── Queries ──────────────────────────────────────────────────────────────

        public static string CurrentBranch()
        {
            var r = Run("rev-parse", "--abbrev-ref", "HEAD");
            return r.Ok ? r.StdOut.Trim() : string.Empty;
        }

        public static bool IsProtectedBranch(string branch)
        {
            if (string.IsNullOrEmpty(branch)) return true;
            foreach (var p in ProtectedBranches)
                if (string.Equals(branch, p, StringComparison.OrdinalIgnoreCase)) return true;
            return branch.StartsWith("release/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Working-tree changes, optionally narrowed to a pathspec (e.g. <c>"Assets"</c>).
        /// Untracked files are included individually — a tool's brand new asset is untracked,
        /// and that is precisely the case that gets forgotten.
        /// </summary>
        public static List<FrogletGitChange> Status(string pathPrefix = null)
        {
            var list = new List<FrogletGitChange>();

            // core.quotepath=false keeps non-ASCII paths readable instead of octal-escaped.
            var r = string.IsNullOrEmpty(pathPrefix)
                ? Run("-c", "core.quotepath=false", "status", "--porcelain", "--untracked-files=all")
                : Run("-c", "core.quotepath=false", "status", "--porcelain", "--untracked-files=all", "--", pathPrefix);

            if (!r.Ok) return list;

            foreach (var raw in r.StdOut.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length < 4) continue;

                var code = line.Substring(0, 2);
                var path = line.Substring(3).Trim();

                // Renames arrive as "old -> new"; the destination is what we stage.
                var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0) path = path.Substring(arrow + 4);

                list.Add(new FrogletGitChange(code, Unquote(path).Replace('\\', '/')));
            }

            return list;
        }

        /// <summary>
        /// Did this commit fail only because there was nothing to record? That is a normal
        /// outcome, not an error.
        /// </summary>
        public static bool NothingToCommit(FrogletGitResult r)
            => r.Text.IndexOf("nothing to commit", StringComparison.OrdinalIgnoreCase) >= 0
               || r.Text.IndexOf("no changes added to commit", StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Mutations ────────────────────────────────────────────────────────────

        /// <summary>Stages exactly the given paths. Never a wildcard, never <c>-A</c>.</summary>
        public static FrogletGitResult Add(IEnumerable<string> paths)
        {
            var args = new List<string> { "add", "--" };
            var any = false;
            foreach (var p in paths)
            {
                if (string.IsNullOrWhiteSpace(p)) continue;
                args.Add(p);
                any = true;
            }
            if (!any) return new FrogletGitResult("git add (no paths)", 0, string.Empty, string.Empty);
            return Run(args.ToArray());
        }

        /// <summary>
        /// Commit, limited to <paramref name="paths"/> when given.
        ///
        /// The pathspec is load-bearing, not decoration: a bare <c>git commit</c> records the
        /// WHOLE index, so if the human had already staged something of their own before pressing
        /// the button, a tool would sweep it into its commit — breaking the "only my own output"
        /// promise at the last step, after `add` had been careful about it all along. With the
        /// pathspec, their staged work stays staged and untouched.
        /// </summary>
        public static FrogletGitResult Commit(string message, IEnumerable<string> paths = null)
        {
            var args = new List<string> { "commit", "-m", message };

            if (paths != null)
            {
                var scoped = new List<string>();
                foreach (var p in paths)
                    if (!string.IsNullOrWhiteSpace(p))
                        scoped.Add(p);

                if (scoped.Count > 0)
                {
                    args.Add("--");
                    args.AddRange(scoped);
                }
            }

            return Run(args.ToArray());
        }

        public static FrogletGitResult Push(string branch) => Run("push", "-u", "origin", branch);

        /// <summary>
        /// Push with exponential backoff on NETWORK failures only. An auth failure or a rejected
        /// non-fast-forward is not retried — repeating it just burns time and hides the message.
        /// </summary>
        public static FrogletGitResult PushWithRetry(string branch, int attempts = 3)
        {
            var result = new FrogletGitResult("git push", -1, string.Empty, "push never ran");
            var delayMs = 2000;

            for (var i = 0; i < Mathf.Max(1, attempts); i++)
            {
                result = Push(branch);
                if (result.Ok) return result;
                if (!LooksLikeNetworkFailure(result)) return result;
                if (i < attempts - 1)
                {
                    Thread.Sleep(delayMs);
                    delayMs *= 2;
                }
            }

            return result;
        }

        static bool LooksLikeNetworkFailure(FrogletGitResult r)
        {
            var t = r.Text.ToLowerInvariant();
            return t.Contains("could not resolve host")
                   || t.Contains("unable to access")
                   || t.Contains("connection reset")
                   || t.Contains("connection timed out")
                   || t.Contains("operation timed out")
                   || t.Contains("failed to connect")
                   || t.Contains("network is unreachable")
                   || t.Contains("timed out");
        }

        // ── Argument quoting ─────────────────────────────────────────────────────

        static readonly char[] NeedsQuoting = { ' ', '\t', '"', '\\', '\n', '\'' };

        static string BuildArguments(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (a == null) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(a));
            }
            return sb.ToString();
        }

        static string Quote(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(NeedsQuoting) < 0) return arg;

            var sb = new StringBuilder(arg.Length + 8);
            sb.Append('"');

            var backslashes = 0;
            foreach (var c in arg)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes > 0) sb.Append('\\', backslashes);
                backslashes = 0;
                sb.Append(c);
            }

            if (backslashes > 0) sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        static string Unquote(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return s;
        }
    }
}
