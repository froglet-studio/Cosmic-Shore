using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>What one fetch of the status branch came back with. Plain data, built off the main thread.</summary>
    public sealed class LaunchFetchResult
    {
        public string Json;
        /// <summary>The status branch commit the JSON was read from (short), if known.</summary>
        public string Commit;
        /// <summary>True when the fetch failed and the JSON is the last copy this checkout already had.</summary>
        public bool FromCache;
        /// <summary>Why nothing (or only the cached copy) could be read; null on a clean fetch.</summary>
        public string Error;
        public DateTime FinishedUtc;
    }

    /// <summary>
    /// Reads <c>status.json</c> from the launch-status branch with <c>git fetch origin &lt;branch&gt;</c>
    /// then <c>git show FETCH_HEAD:&lt;file&gt;</c>, on a thread-pool thread, so the window's OnGUI never
    /// waits on the network. Touches no Unity API off the main thread: the repo root is captured by
    /// the caller, and the result is plain strings the window picks up on its own update tick.
    /// Writes nothing but git's own fetch bookkeeping (FETCH_HEAD and the remote-tracking ref).
    /// </summary>
    public static class LaunchStatusFetcher
    {
        static readonly KeyValuePair<string, string>[] NoPrompt =
        {
            // Never stop for a credential prompt: the repo is public, and a prompt would hang the fetch.
            new KeyValuePair<string, string>("GIT_TERMINAL_PROMPT", "0"),
        };

        public static Task<LaunchFetchResult> FetchAsync(string repoRoot, string branch, string file) =>
            Task.Run(() => Fetch(repoRoot, branch, file));

        static LaunchFetchResult Fetch(string repoRoot, string branch, string file)
        {
            var result = new LaunchFetchResult();
            try
            {
                var fetch = FrogletGit.RunInWithEnv(repoRoot, NoPrompt, "fetch", "--no-tags", "origin", branch);
                if (fetch.Ok)
                {
                    var show = FrogletGit.RunIn(repoRoot, "show", "FETCH_HEAD:" + file);
                    if (show.Ok)
                    {
                        result.Json = show.StdOut;
                        var head = FrogletGit.RunIn(repoRoot, "rev-parse", "--short", "FETCH_HEAD");
                        if (head.Ok) result.Commit = head.StdOut.Trim();
                    }
                    else result.Error = "The " + branch + " branch has no " + file + " yet.";
                }
                else
                {
                    // Offline or no such branch: fall back to the copy this checkout already has.
                    var cached = FrogletGit.RunIn(repoRoot, "show", "origin/" + branch + ":" + file);
                    if (cached.Ok)
                    {
                        result.Json = cached.StdOut;
                        result.FromCache = true;
                        var head = FrogletGit.RunIn(repoRoot, "rev-parse", "--short", "origin/" + branch);
                        if (head.Ok) result.Commit = head.StdOut.Trim();
                    }
                    result.Error = "Could not fetch " + branch + ": " + FirstLine(fetch.StdErr);
                }
            }
            catch (Exception e)
            {
                // git missing from PATH, or the process could not start.
                result.Error = "Could not run git: " + e.Message;
            }
            result.FinishedUtc = DateTime.UtcNow;
            return result;
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "no reason given.";
            foreach (var line in s.Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            return s.Trim();
        }
    }
}
