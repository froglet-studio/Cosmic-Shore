using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>The commit a workspace is sitting on.</summary>
    public sealed record CommitInfo(string Sha, string Subject, string Author, string When);

    /// <summary>
    /// The checkout the launcher builds and runs from. It is never the tester's own working copy:
    /// either a shallow clone the launcher owns, or a git worktree beside their clone (which
    /// shares its objects, so switching branches costs only the files that differ).
    /// </summary>
    public sealed class Workspace
    {
        readonly LauncherSettings _s;
        readonly Toolchain _tools;

        public Workspace(LauncherSettings s, Toolchain tools) { _s = s; _tools = tools; }

        public string Dir => _s.Workspace == WorkspaceMode.WorktreeOfMyClone && !string.IsNullOrWhiteSpace(_s.MyClonePath)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_s.MyClonePath).TrimEnd('\\', '/'))!,
                Path.GetFileName(Path.GetFullPath(_s.MyClonePath).TrimEnd('\\', '/')) + "-froglet-engine")
            : _s.ResolvedManagedPath;

        public bool Exists => Directory.Exists(Path.Combine(Dir, ".git")) || File.Exists(Path.Combine(Dir, ".git"));
        public bool HasEngine => File.Exists(Path.Combine(Dir, "Port", "src", "CosmicShore.Player", "CosmicShore.Player.csproj"));
        public string PlayerProject => Path.Combine(Dir, "Port", "src", "CosmicShore.Player", "CosmicShore.Player.csproj");
        public string BuildProject => Path.Combine(Dir, "Port", "src", "CosmicShore.Build", "CosmicShore.Build.csproj");

        Dictionary<string, string> GitEnv()
        {
            var env = new Dictionary<string, string>
            {
                // Only the engine's own natives are LFS objects; skipping the smudge keeps a clone to
                // the files the game reads, and FetchNatives pulls the two libraries it needs.
                ["GIT_LFS_SKIP_SMUDGE"] = "1",
            };
            if (!string.IsNullOrWhiteSpace(_s.GitHubToken))
            {
                // Passed as config through the environment, so the token never appears on a command line or in .git/config.
                env["GIT_CONFIG_COUNT"] = "1";
                env["GIT_CONFIG_KEY_0"] = "http.https://github.com/.extraheader";
                env["GIT_CONFIG_VALUE_0"] = "AUTHORIZATION: basic " + TokenAuth();
            }
            return env;
        }

        string TokenAuth() => Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + _s.GitHubToken.Trim()));

        string Git => _tools.Git ?? throw new InvalidOperationException("git was not found.");

        /// <summary>Every branch on the remote, newest naming first (no clone needed).</summary>
        public async Task<List<string>> ListBranches(LogBuffer log, CancellationToken ct)
        {
            var r = await ProcessRunner.Run(Git, new[] { "ls-remote", "--heads", _s.RemoteUrl }, null, log, ct, GitEnv(), quiet: true);
            if (r.ExitCode != 0)
            {
                log.Add(LogKind.Error, "Could not list branches: " + FirstLine(r.StdErr) + "  (private repo? paste a GitHub token in OPTIONS > SOURCE)");
                return new List<string>();
            }
            return r.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Split('\t'))
                .Where(p => p.Length == 2 && p[1].StartsWith("refs/heads/"))
                .Select(p => p[1]["refs/heads/".Length..].Trim())
                .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Brings the workspace to the tip of <paramref name="branch"/>. Reports 0..1 through <paramref name="progress"/>.</summary>
        public async Task<bool> Sync(string branch, LogBuffer log, Action<float, string> progress, CancellationToken ct)
        {
            void OnGit(string line)
            {
                var m = Regex.Match(line, @"(Receiving objects|Resolving deltas|Updating files|Checking out files):\s+(\d+)%");
                if (!m.Success) return;
                float pct = int.Parse(m.Groups[2].Value) / 100f;
                float weight = m.Groups[1].Value switch { "Receiving objects" => 0.7f, "Resolving deltas" => 0.1f, _ => 0.2f };
                float offset = m.Groups[1].Value switch { "Receiving objects" => 0f, "Resolving deltas" => 0.7f, _ => 0.8f };
                progress(offset + pct * weight, m.Groups[1].Value);
            }

            if (_s.Workspace == WorkspaceMode.WorktreeOfMyClone)
            {
                var clone = _s.MyClonePath;
                if (!Directory.Exists(Path.Combine(clone, ".git")))
                {
                    log.Add(LogKind.Error, $"\"{clone}\" is not a git clone. Pick your Cosmic Shore folder in Options.");
                    return false;
                }
                progress(0, "Fetching " + branch);
                var f = await ProcessRunner.Run(Git, new[] { "-C", clone, "fetch", "--progress", "origin", branch }, null, log, ct, GitEnv(), OnGit);
                if (f.ExitCode != 0) return Fail(log, "git fetch failed. Check the branch name; for a private repository sign in to GitHub or paste a token in OPTIONS > SOURCE.");
                var sha = (await ProcessRunner.Run(Git, new[] { "-C", clone, "rev-parse", "FETCH_HEAD" }, null, log, ct, GitEnv(), quiet: true)).StdOut.Trim();
                if (!Exists)
                {
                    progress(0.8f, "Creating workspace");
                    var w = await ProcessRunner.Run(Git, new[] { "-C", clone, "worktree", "add", "--detach", Dir, sha }, null, log, ct, GitEnv(), OnGit);
                    if (w.ExitCode != 0) return Fail(log, "git worktree add failed.");
                }
                else
                {
                    var c = await ProcessRunner.Run(Git, new[] { "-C", Dir, "checkout", "--detach", "--force", sha }, null, log, ct, GitEnv(), OnGit);
                    if (c.ExitCode != 0) return Fail(log, "git checkout failed.");
                }
            }
            else
            {
                if (!Exists)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Dir)!);
                    if (Directory.Exists(Dir) && Directory.EnumerateFileSystemEntries(Dir).Any())
                        return Fail(log, $"{Dir} exists and is not a clone. Empty it or choose another folder in Options.");
                    log.Add(LogKind.Info, "First run: downloading the project (about 1.5 GB, once).");
                    progress(0, "Cloning " + branch);
                    var r = await ProcessRunner.Run(Git, new[] { "clone", "--progress", "--depth", "1", "--single-branch",
                        "--branch", branch, _s.RemoteUrl, Dir }, null, log, ct, GitEnv(), OnGit);
                    if (r.ExitCode != 0) return Fail(log, "git clone failed. If the repository is private and git has no GitHub sign-in, paste a GitHub token in OPTIONS > SOURCE.");
                }
                else
                {
                    progress(0, "Fetching " + branch);
                    var f = await ProcessRunner.Run(Git, new[] { "-C", Dir, "fetch", "--progress", "--depth", "1", "origin", branch }, null, log, ct, GitEnv(), OnGit);
                    if (f.ExitCode != 0) return Fail(log, "git fetch failed (check the branch name and your connection).");
                    progress(0.8f, "Checking out");
                    var c = await ProcessRunner.Run(Git, new[] { "-C", Dir, "checkout", "--force", "--detach", "FETCH_HEAD" }, null, log, ct, GitEnv(), OnGit);
                    if (c.ExitCode != 0) return Fail(log, "git checkout failed.");
                    // Untracked leftovers from another branch are removed; ignored build output (bin/obj, natives) stays, so rebuilds are incremental.
                    await ProcessRunner.Run(Git, new[] { "-C", Dir, "clean", "-fdq" }, null, log, ct, quiet: true);
                }
            }
            progress(1, "Up to date");
            if (!HasEngine)
                return Fail(log, $"Branch \"{branch}\" does not contain the engine (Port/). Pick a branch that does.");
            return true;
        }

        public CommitInfo? Commit()
        {
            if (!Exists || _tools.Git == null) return null;
            var o = ProcessRunner.Capture(_tools.Git, "-C", Dir, "log", "-1", "--format=%h%x1f%s%x1f%an%x1f%cr");
            if (o == null) return null;
            var p = o.Split('\x1f');
            return p.Length == 4 ? new CommitInfo(p[0], p[1], p[2], p[3]) : null;
        }

        /// <summary>Scenes listed in the project's Build Settings (what a tester can start in).</summary>
        /// <summary>The enabled build scenes: the engine's own list when authored, else Unity's.</summary>
        public List<string> BuildScenes()
        {
            if (!File.Exists(Path.Combine(Dir, "ProjectSettings", "EditorBuildSettings.asset"))) return new();
            var p = CosmicShore.Froglet.FrogletProjectSettings.Load(Dir);
            return p.BuildScenes(new CosmicShore.Froglet.FrogletProjectSettings.UnityDefaults(Dir))
                .Where(s => s.Enabled).Select(s => Path.GetFileNameWithoutExtension(s.Path)).ToList();
        }

        // ---------------------------------------------------------------- FMOD natives (Git LFS)

        static readonly Regex Pointer = new(@"^version https://git-lfs\.github\.com/spec/v1\noid sha256:([0-9a-f]{64})\nsize (\d+)\n?$");

        /// <summary>
        /// The engine plays FMOD audio through the vendor's native library, which the repository
        /// stores in Git LFS. This fetches just that library (Port/.native/&lt;platform&gt;) the same
        /// way Port/tools/fetch_native.py does. Returns false when audio has to stay off.
        /// </summary>
        public async Task<bool> FetchNatives(LogBuffer log, CancellationToken ct)
        {
            (string rid, string src, string lib)? plat =
                OperatingSystem.IsWindows() ? ("win-x64", "Assets/Plugins/FMOD/platforms/win/lib/x86_64", "fmodstudio.dll")
                : OperatingSystem.IsLinux() ? ("linux-x64", "Assets/Plugins/FMOD/platforms/linux/lib/x86_64", "libfmodstudio.so")
                : null;
            if (plat == null) { log.Add(LogKind.Warn, "No FMOD library for this OS yet; the game runs silent."); return false; }
            var (rid, srcDir, lib) = plat.Value;
            var source = Path.Combine(Dir, srcDir, lib);
            var dest = Path.Combine(Dir, "Port", ".native", rid, lib);
            if (!File.Exists(source)) { log.Add(LogKind.Warn, "FMOD library not in this branch; the game runs silent."); return false; }

            var head = File.ReadAllBytes(source);
            var text = head.Length < 400 ? Encoding.ASCII.GetString(head) : "";
            var m = Pointer.Match(text);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!m.Success)
            {
                File.Copy(source, dest, overwrite: true);
                return true;
            }
            string oid = m.Groups[1].Value;
            long size = long.Parse(m.Groups[2].Value);
            if (File.Exists(dest) && Sha256(dest) == oid) return true;

            log.Add(LogKind.Info, $"Fetching the FMOD audio library ({size / 1024 / 1024.0:0.0} MB)");
            try
            {
                var url = _s.RemoteUrl.EndsWith(".git") ? _s.RemoteUrl : _s.RemoteUrl + ".git";
                var body = JsonSerializer.Serialize(new { operation = "download", transfers = new[] { "basic" },
                    objects = new[] { new { oid, size } } });
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                async Task<HttpResponseMessage> Ask(AuthenticationHeaderValue? auth)
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, url + "/info/lfs/objects/batch")
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/vnd.git-lfs+json"),
                    };
                    req.Headers.Accept.ParseAdd("application/vnd.git-lfs+json");
                    if (auth != null) req.Headers.Authorization = auth;
                    return await http.SendAsync(req, ct);
                }
                var resp = await Ask(null);
                if (!resp.IsSuccessStatusCode)
                {
                    // Private repository: borrow the credentials git already has (GitHub Desktop / Credential Manager).
                    var cred = CredentialFill(new Uri(url));
                    if (!string.IsNullOrWhiteSpace(_s.GitHubToken)) cred = TokenAuth();
                    if (cred != null) resp = await Ask(new AuthenticationHeaderValue("Basic", cred));
                }
                if (!resp.IsSuccessStatusCode) throw new Exception($"LFS answered {(int)resp.StatusCode}");
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var action = doc.RootElement.GetProperty("objects")[0].GetProperty("actions").GetProperty("download");
                var dl = new HttpRequestMessage(HttpMethod.Get, action.GetProperty("href").GetString());
                if (action.TryGetProperty("header", out var headers))
                    foreach (var h in headers.EnumerateObject()) dl.Headers.TryAddWithoutValidation(h.Name, h.Value.GetString());
                var bytes = await (await http.SendAsync(dl, ct)).Content.ReadAsByteArrayAsync(ct);
                if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != oid) throw new Exception("checksum mismatch");
                await File.WriteAllBytesAsync(dest, bytes, ct);
                log.Add(LogKind.Success, "FMOD audio library ready.");
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Add(LogKind.Warn, $"Could not fetch the FMOD library ({ex.Message}); the game will run silent.");
                return false;
            }
        }

        /// <summary>
        /// A GitHub token for the API: the one typed in OPTIONS, else the password git's credential
        /// helper (GitHub Desktop / Git Credential Manager) already stores for github.com.
        /// </summary>
        public string? GitHubApiToken()
        {
            if (!string.IsNullOrWhiteSpace(_s.GitHubToken)) return _s.GitHubToken.Trim();
            var basic = CredentialFill(new Uri("https://github.com"));
            if (basic == null) return null;
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(basic));
            int i = pair.IndexOf(':');
            return i > 0 ? pair[(i + 1)..] : null;
        }

        string? CredentialFill(Uri remote)
        {
            try
            {
                var psi = new ProcessStartInfo(Git, "credential fill")
                {
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                };
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi)!;
                p.StandardInput.Write($"protocol={remote.Scheme}\nhost={remote.Host}\n\n");
                p.StandardInput.Close();
                var outText = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(15000)) { p.Kill(); return null; }
                string? user = null, pass = null;
                foreach (var l in outText.Split('\n'))
                {
                    if (l.StartsWith("username=")) user = l[9..].Trim();
                    if (l.StartsWith("password=")) pass = l[9..].Trim();
                }
                return user != null && pass != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + pass)) : null;
            }
            catch { return null; }
        }

        static string Sha256(string path)
        {
            using var f = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
        }

        static bool Fail(LogBuffer log, string message) { log.Add(LogKind.Error, message); return false; }
        static string FirstLine(string s) => s.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
    }
}
