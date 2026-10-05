using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Launcher versions, never applied on their own. <see cref="Check"/> says whether the selected
    /// branch has a newer launcher; the user presses UPDATE, or installs any branch, tag or commit.
    /// A version is built from that revision's own source (exported with git archive, published
    /// with the launcher's dotnet) into its own folder, so every installed version stays available
    /// and USE switches between them - the running .exe is swapped and the launcher restarts.
    /// </summary>
    public sealed class LauncherUpdater
    {
        static readonly string[] SourcePaths = { "Port/src/CosmicShore.Launcher", "Port/src/Shared", "Port/Directory.Build.props" };

        public sealed record VersionInfo(string Commit, string Date, string Subject, string Source, string Dir)
        {
            public string Short => Commit.Length >= 7 ? Commit[..7] : Commit;
        }

        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly Workspace _ws;

        public LauncherUpdater(LauncherSettings s, Toolchain tools, Workspace ws) { _s = s; _tools = tools; _ws = ws; }

        public static string Commit { get; } = Meta("LauncherCommit");
        public static string Date { get; } = Meta("LauncherDate");
        public static string Short => Commit.Length >= 7 ? Commit[..7] : "dev";

        static string Meta(string key) =>
            typeof(LauncherUpdater).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value?.Trim() ?? "";

        public static string VersionsDir => Path.Combine(LauncherSettings.DataDir, "versions");

        /// <summary>The newest launcher on the selected branch, when it differs from this one.</summary>
        public VersionInfo? Available { get; private set; }
        public List<string> Changes { get; } = new();
        public bool Checking { get; private set; }
        public DateTime CheckedAt { get; private set; }

        public bool Installing { get; private set; }
        public float Progress { get; private set; }
        public string Phase { get; private set; } = "";
        public string? Error { get; private set; }
        public bool Done { get; private set; }

        string Git => _tools.Git ?? "git";

        public void ClearError() => Error = null;

        /// <summary>The launcher's own change list between two versions (for the "updated" splash).</summary>
        public System.Collections.Generic.IEnumerable<string> ChangesBetween(string from, string to)
        {
            if (!_ws.Exists || _tools.Git == null) return Array.Empty<string>();
            var log = GitOut(new[] { "log", "--format=%s", "-n", "6", $"{from}..{to}", "--" }.Concat(SourcePaths).ToArray());
            return log?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        }

        string? GitOut(params string[] args) => ProcessRunner.Capture(Git, new[] { "-C", _ws.Dir }.Concat(args).ToArray());

        /// <summary>Fetches the branch (unless told not to) and compares its newest launcher commit with this build's.</summary>
        public async Task Check(bool fetch = true)
        {
            if (Checking || !_ws.Exists || _tools.Git == null) return;
            Checking = true;
            try
            {
                if (fetch) await ProcessRunner.Run(Git, new[] { "-C", _ws.Dir, "fetch", "--quiet", "origin", _s.Branch }, null, null, CancellationToken.None, quiet: true);
                var rev = "origin/" + _s.Branch;
                var info = Describe(rev);
                Changes.Clear();
                if (info == null || info.Commit == Commit) { Available = null; return; }
                if (Commit.Length > 0)
                {
                    var log = GitOut(new[] { "log", "--format=%h  %s", "-n", "12", $"{Commit}..{rev}", "--" }.Concat(SourcePaths).ToArray());
                    if (log != null) Changes.AddRange(log.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                    // This launcher is newer than the branch (someone runs a feature build): nothing to offer.
                    if (log != null && Changes.Count == 0) { Available = null; return; }
                }
                Available = info with { Source = _s.Branch };
            }
            finally { Checking = false; CheckedAt = DateTime.Now; }
        }

        /// <summary>The last commit at or before <paramref name="rev"/> that changed the launcher.</summary>
        VersionInfo? Describe(string rev)
        {
            var line = GitOut(new[] { "log", "-1", "--format=%H|%cs|%s", rev, "--" }.Concat(SourcePaths).ToArray());
            if (string.IsNullOrWhiteSpace(line)) return null;
            var p = line.Split('|', 3);
            return p.Length < 3 ? null : new VersionInfo(p[0], p[1], p[2], rev, "");
        }

        public static List<VersionInfo> Installed()
        {
            var list = new List<VersionInfo>();
            if (!Directory.Exists(VersionsDir)) return list;
            foreach (var dir in Directory.GetDirectories(VersionsDir))
            {
                try
                {
                    var v = JsonSerializer.Deserialize<VersionInfo>(File.ReadAllText(Path.Combine(dir, "version.json")));
                    if (v != null && File.Exists(Exe(dir))) list.Add(v with { Dir = dir });
                }
                catch { }
            }
            return list.OrderByDescending(v => v.Date).ThenBy(v => v.Commit).ToList();
        }

        static string Exe(string dir) => Path.Combine(dir, OperatingSystem.IsWindows() ? "FrogletLauncher.exe" : "FrogletLauncher");

        /// <summary>The running launcher, when it is a file that can be swapped (not `dotnet FrogletLauncher.dll`).</summary>
        static string? RunningExe
        {
            get
            {
                var p = Environment.ProcessPath;
                return p != null && !Path.GetFileNameWithoutExtension(p).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? p : null;
            }
        }

        /// <summary>Builds the launcher of <paramref name="rev"/> (a branch, tag or commit) into its own version folder, then switches to it.</summary>
        public async Task Install(string rev, LogBuffer log)
        {
            if (Installing) return;
            Installing = true; Done = false; Error = null; Progress = 0;
            try
            {
                if (_tools.Git == null || _tools.Dotnet == null) throw new Exception("git and .NET are needed (press START once to set them up).");
                if (!_ws.Exists) throw new Exception("No workspace yet: press START once.");
                bool commit = System.Text.RegularExpressions.Regex.IsMatch(rev, "^[0-9a-fA-F]{7,40}$");
                Phase = "Fetching " + (commit ? rev[..7] : rev);
                // A branch or tag: fetch just that. A commit already here needs no network.
                if (!commit || GitOut("cat-file", "-e", rev + "^{commit}") == null)
                    await ProcessRunner.Run(Git, new[] { "-C", _ws.Dir, "fetch", "--quiet", "origin", commit ? "" : rev }.Where(x => x.Length > 0).ToArray(), null, log, CancellationToken.None);
                string resolved = GitOut("rev-parse", "--verify", "--quiet", "origin/" + rev) != null ? "origin/" + rev : rev;
                var info = Describe(resolved) ?? throw new Exception($"'{rev}' is not a branch, tag or commit that has the launcher.");
                Progress = 0.08f;

                var dir = Path.Combine(VersionsDir, info.Short);
                if (!File.Exists(Exe(dir)))
                {
                    Phase = "Exporting the source";
                    var src = Path.Combine(LauncherSettings.DataDir, "build", "launcher-" + info.Short);
                    if (Directory.Exists(src)) Directory.Delete(src, true);
                    Directory.CreateDirectory(src);
                    var zip = src + ".zip";
                    var r = await ProcessRunner.Run(Git, new[] { "-C", _ws.Dir, "archive", "--format=zip", "-o", zip, info.Commit }.Concat(SourcePaths).ToArray(), null, log, CancellationToken.None);
                    if (r.ExitCode != 0) throw new Exception("git archive failed - see CONSOLE.");
                    ZipFile.ExtractToDirectory(zip, src);
                    File.Delete(zip);
                    Progress = 0.14f;

                    Phase = "Compiling the launcher";
                    var tmp = dir + ".partial";
                    if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    int lines = 0;
                    r = await ProcessRunner.Run(_tools.Dotnet, new[]
                    {
                        "publish", Path.Combine(src, "Port", "src", "CosmicShore.Launcher"), "-c", "Release",
                        "-r", RuntimeInformation.RuntimeIdentifier, "--self-contained",
                        "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
                        "-p:LauncherCommit=" + info.Commit, "-p:LauncherDate=" + info.Date, "-o", tmp,
                    }, null, log, CancellationToken.None, _tools.DotnetEnv(), onLine: l =>
                    {
                        lines++;
                        if (l.Contains("Restored") || l.Contains("Determining projects")) { Phase = "Restoring packages"; Progress = Math.Max(Progress, 0.3f); }
                        else if (l.Contains(" -> ") && l.Contains(".dll")) { Phase = "Packing a single file"; Progress = Math.Max(Progress, 0.8f); }
                        else Progress = Math.Max(Progress, Math.Min(0.78f, 0.3f + lines * 0.02f));
                    });
                    if (r.ExitCode != 0 || !File.Exists(Exe(tmp))) throw new Exception("The launcher did not compile at " + info.Short + " - see CONSOLE.");
                    foreach (var f in Directory.GetFiles(tmp, "*.pdb")) File.Delete(f);
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                    Directory.Move(tmp, dir);
                    File.WriteAllText(Path.Combine(dir, "version.json"), JsonSerializer.Serialize(info with { Source = rev, Dir = "" }));
                    try { Directory.Delete(src, true); } catch { }
                }
                Progress = 0.92f;
                Phase = "Installing";
                Use(Installed().First(v => v.Commit == info.Commit), log);
            }
            catch (Exception e)
            {
                Error = e.Message;
                log.Add(LogKind.Error, "Launcher update: " + e.Message);
                Installing = false;
            }
        }

        /// <summary>
        /// Puts an installed version in place of the running launcher and restarts into it. The
        /// running one is kept as a version first, so going back is always one USE away.
        /// </summary>
        public void Use(VersionInfo v, LogBuffer log)
        {
            var running = RunningExe;
            if (running == null) { Error = "This launcher runs through `dotnet`; start " + Exe(v.Dir) + " directly."; Installing = false; return; }
            KeepRunning(running);
            Phase = "Restarting";
            Progress = 1f;
            Done = true;
            log.Add(LogKind.Success, $"Launcher {v.Short} ({v.Date}) installed - restarting.");
            // Never touch the running file: a single-file app keeps reading its own .exe. The new
            // version waits for this one to exit, copies itself into place and starts from there.
            Process.Start(new ProcessStartInfo(Exe(v.Dir), new[] { "--install-to", running, "--wait", Environment.ProcessId.ToString(), "--updated", Short })
                { UseShellExecute = false });
        }

        /// <summary>Saves the running launcher as a version (once), so it can be switched back to.</summary>
        static void KeepRunning(string running)
        {
            if (Commit.Length == 0) return;
            var dir = Path.Combine(VersionsDir, Short);
            if (File.Exists(Exe(dir))) return;
            try
            {
                Directory.CreateDirectory(dir);
                File.Copy(running, Exe(dir), overwrite: true);
                File.WriteAllText(Path.Combine(dir, "version.json"),
                    JsonSerializer.Serialize(new VersionInfo(Commit, Date, "the version you had before", "kept", "")));
            }
            catch { }
        }

        /// <summary>
        /// The second half of a switch, run by the NEW version from its versions folder: wait for the
        /// old launcher to exit, copy this .exe over it, start it. Returns the exit code.
        /// </summary>
        public static int FinishInstall(string target, int waitPid, string? updatedFrom)
        {
            try { using var old = Process.GetProcessById(waitPid); old.WaitForExit(30000); } catch { /* already gone */ }
            var self = Environment.ProcessPath!;
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Copy(self, target, overwrite: true);
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                                     UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    break;
                }
                catch (IOException) when (attempt < 40) { System.Threading.Thread.Sleep(250); } // the old file can stay locked a moment
                catch (UnauthorizedAccessException) when (attempt < 40) { System.Threading.Thread.Sleep(250); }
            }
            var args = updatedFrom != null ? new[] { "--updated", updatedFrom } : Array.Empty<string>();
            Process.Start(new ProcessStartInfo(target, args) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)! });
            return 0;
        }
    }
}
