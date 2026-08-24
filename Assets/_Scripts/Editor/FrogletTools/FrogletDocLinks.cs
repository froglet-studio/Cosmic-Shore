using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Opens a tool's documentation — the DOCS chip on every Froglet Master Tool card and the
    /// per-window Docs buttons route through here.
    ///
    /// A doc is addressed by its REPO-RELATIVE path (optionally with a <c>#anchor</c>), never by a
    /// hardcoded URL: the GitHub link is built from the checkout's own <c>.git/config</c> origin,
    /// so forks and remote renames keep working, and when no GitHub remote resolves (offline, odd
    /// remote) the local file opens instead — the documentation is in the repo either way.
    /// </summary>
    public static class FrogletDocLinks
    {
        /// <summary>Docs are linked on the integration branch — the newest state of every doc.</summary>
        public const string DefaultBranch = "bleeding-edge";

        static string _webRoot;
        static bool _webRootResolved;

        /// <summary>Opens the doc on GitHub, or the local file when no remote resolves.</summary>
        public static void Open(string repoRelativePath)
        {
            if (string.IsNullOrEmpty(repoRelativePath)) return;

            var url = TryBuildUrl(repoRelativePath);
            if (url != null)
            {
                Application.OpenURL(url);
                return;
            }

            var local = Path.Combine(ProjectRoot(), StripAnchor(repoRelativePath));
            if (File.Exists(local)) EditorUtility.OpenWithDefaultApp(local);
            else Debug.LogWarning($"[FrogletTools] Documentation not found: {repoRelativePath}");
        }

        /// <summary>GitHub blob URL for a repo-relative path (+ optional #anchor), or null when the
        /// origin remote is not a recognisable GitHub URL.</summary>
        public static string TryBuildUrl(string repoRelativePath)
        {
            var root = WebRoot();
            if (root == null) return null;

            var path = repoRelativePath.Replace('\\', '/').TrimStart('/');
            string anchor = "";
            int hash = path.IndexOf('#');
            if (hash >= 0)
            {
                anchor = path[hash..];
                path = path[..hash];
            }

            var sb = new StringBuilder(root.Length + path.Length + 32);
            sb.Append(root).Append("/blob/").Append(DefaultBranch);
            foreach (var segment in path.Split('/'))
                sb.Append('/').Append(Uri.EscapeDataString(segment));
            sb.Append(anchor);
            return sb.ToString();
        }

        // ── Remote resolution ────────────────────────────────────────────────────

        /// <summary>"https://github.com/owner/repo" from .git/config's origin url, cached; null when
        /// unresolvable. Reading the config file directly avoids spawning git per window repaint.</summary>
        static string WebRoot()
        {
            if (_webRootResolved) return _webRoot;
            _webRootResolved = true;
            try
            {
                var config = Path.Combine(ProjectRoot(), ".git", "config");
                if (!File.Exists(config)) return _webRoot = null;

                bool inOrigin = false;
                foreach (var raw in File.ReadAllLines(config))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        inOrigin = line.Replace(" ", "") == "[remote\"origin\"]";
                        continue;
                    }
                    if (!inOrigin || !line.StartsWith("url", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    return _webRoot = ToWebRoot(line[(eq + 1)..].Trim());
                }
            }
            catch { }
            return _webRoot = null;
        }

        /// <summary>Normalizes the two common GitHub remote forms; null for anything else.</summary>
        internal static string ToWebRoot(string remoteUrl)
        {
            if (string.IsNullOrEmpty(remoteUrl)) return null;

            const string ssh = "git@github.com:";
            if (remoteUrl.StartsWith(ssh, StringComparison.OrdinalIgnoreCase))
                return "https://github.com/" + TrimGitSuffix(remoteUrl[ssh.Length..]);

            const string https = "https://github.com/";
            if (remoteUrl.StartsWith(https, StringComparison.OrdinalIgnoreCase))
                return "https://github.com/" + TrimGitSuffix(remoteUrl[https.Length..]);

            return null;
        }

        static string TrimGitSuffix(string ownerRepo)
        {
            ownerRepo = ownerRepo.TrimEnd('/');
            return ownerRepo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? ownerRepo[..^4]
                : ownerRepo;
        }

        static string StripAnchor(string path)
        {
            int hash = path.IndexOf('#');
            return hash >= 0 ? path[..hash] : path;
        }

        static string ProjectRoot()
            => Directory.GetParent(Application.dataPath)!.FullName;
    }
}
