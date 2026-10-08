using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The launcher's one-thing-at-a-time job runner: sync a branch, build the player, start the
    /// game, build a phone package. The UI reads <see cref="Stage"/>/<see cref="Progress"/> each
    /// frame and can cancel. Everything here runs off the UI thread.
    /// </summary>
    public sealed class LauncherJobs
    {
        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly Workspace _ws;
        public readonly LogBuffer Log = new();

        CancellationTokenSource? _cts;
        Process? _game;

        public LauncherJobs(LauncherSettings s, Toolchain tools, Workspace ws) { _s = s; _tools = tools; _ws = ws; }

        public bool Busy { get; private set; }
        public string JobName { get; private set; } = "";
        public string Stage { get; private set; } = "Ready";
        /// <summary>0..1, or negative when the current step has no measurable progress.</summary>
        public float Progress { get; private set; } = -1;
        public bool? LastOk { get; private set; }
        public string? LastArtifact { get; private set; }
        public bool GameRunning => _game is { HasExited: false };
        public CommitInfo? Commit { get; private set; }
        public List<string> Branches { get; private set; } = new();
        public bool BranchesLoading { get; private set; }
        public List<string> Scenes { get; private set; } = new();

        public void RefreshLocalState()
        {
            Commit = _ws.Commit();
            Scenes = _ws.BuildScenes();
        }

        public void Cancel() => _cts?.Cancel();

        public void StopGame()
        {
            try { if (GameRunning) _game!.Kill(entireProcessTree: true); } catch { /* exited meanwhile */ }
        }

        public void LoadBranches()
        {
            if (BranchesLoading || _tools.Git == null) return;
            BranchesLoading = true;
            Task.Run(async () =>
            {
                try { Branches = await _ws.ListBranches(Log, CancellationToken.None); }
                finally { BranchesLoading = false; }
            });
        }

        void Start(string name, Func<CancellationToken, Task<bool>> body)
        {
            if (Busy) return;
            Busy = true; JobName = name; LastOk = null; LastArtifact = null; Progress = -1; Stage = "Starting";
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            Log.Add(LogKind.Info, $"---- {name} ----");
            Task.Run(async () =>
            {
                bool ok = false;
                try { ok = await body(ct); }
                catch (OperationCanceledException) { Log.Add(LogKind.Warn, "Cancelled."); }
                catch (Exception ex) { Log.Add(LogKind.Error, ex.Message); }
                finally
                {
                    LastOk = ok;
                    Stage = ok ? "Done" : "Stopped";
                    Progress = ok ? 1 : -1;
                    Busy = false;
                    try { RefreshLocalState(); } catch { /* best effort */ }
                }
            });
        }

        void Step(string stage, float progress = -1) { Stage = stage; Progress = progress; }

        // ---------------------------------------------------------------- jobs

        public void InstallDotnet() => Start("Install .NET SDK", async ct =>
        {
            Step("Installing .NET SDK");
            bool ok = await _tools.InstallDotnet(Log, ct);
            _tools.Detect(_s);
            return ok && _tools.Dotnet != null;
        });

        public void Update() => Start("Update workspace", async ct => await SyncStep(ct));

        public void Play() => Start("Start game", async ct =>
        {
            if (!await EnsureTools(ct)) return false;
            // Unsaved edits in the workspace are what the user wants to try: build them as they are.
            int pending = _ws.HasEngine ? _ws.PendingChanges() : 0;
            if (pending > 0)
                Log.Add(LogKind.Warn, $"Using the workspace as it is: it has {pending} unsaved change{(pending == 1 ? "" : "s")}, so {_s.Branch} was not pulled. " +
                                      "Commit them to a branch or discard them on the GIT page to get the latest again.");
            else if (_s.PullBeforePlay || !_ws.HasEngine)
                if (!await SyncStep(ct)) return false;
            Step("Audio library");
            bool audio = _s.Audio && await _ws.FetchNatives(Log, ct);
            if (!await BuildPlayer(ct)) return false;
            return LaunchGame(audio);
        });

        /// <summary>
        /// EDITOR > MODELS' VIEW IN ENGINE: the player built as PLAY builds it, opened on one model
        /// (<c>--view-model</c>): drawn by the engine with the game's materials, turned with the mouse.
        /// No game scene loads, so it opens in seconds once the player is built.
        /// </summary>
        public void ViewModel(string projectRelative) => Start("View model", async ct =>
        {
            if (!await EnsureTools(ct)) return false;
            if (!File.Exists(Path.Combine(_ws.Dir, "Port", "src", "CosmicShore.Player", "ModelViewer.cs")))
            {
                Log.Add(LogKind.Error, $"{_s.Branch}'s engine has no model viewer yet: play a branch that has Port/src/CosmicShore.Player/ModelViewer.cs.");
                return false;
            }
            if (!await BuildPlayer(ct)) return false;
            Step("Opening the model viewer", 1);
            var psi = new ProcessStartInfo(PlayerExe)
            {
                WorkingDirectory = Path.Combine(_ws.Dir, "Port"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in new[] { "--view-model", projectRelative, "--size", "1280x800" }) psi.ArgumentList.Add(a);
            foreach (var kv in _tools.DotnetEnv()) psi.Environment[kv.Key] = kv.Value;
            psi.Environment["COSMIC_SHORE_PROJECT"] = _ws.Dir;
            psi.Environment["COSMIC_SHORE_AUDIO"] = "off";
            psi.Environment["COSMIC_SHORE_NET"] = "off";
            if (_s.MobileRenderPath) psi.Environment["COSMIC_SHORE_GLES"] = "1";
            var viewer = new Process { StartInfo = psi, EnableRaisingEvents = true };
            viewer.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            viewer.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            viewer.Start();
            viewer.BeginOutputReadLine();
            viewer.BeginErrorReadLine();
            Log.Add(LogKind.Success, "Model viewer open: drag to turn, wheel to zoom, right-drag to pan, F frame, R reset, Space spin, Tab the next prefab's materials.");
            return true;
        });

        /// <summary>The BUILD page's iOS button, in whichever mode SETTINGS chose.</summary>
        public void BuildIos()
        {
            if (_s.Ios == IosMode.GitHub) BuildIosOnGitHub();
            else BuildPhone(ios: true);
        }

        public string? IosRunUrl { get; private set; }

        /// <summary>No Mac: GitHub's free macOS runner compiles the .ipa from the branch; we download it.</summary>
        public void BuildIosOnGitHub() => Start("Build iOS on GitHub", async ct =>
        {
            IosRunUrl = null;
            Step("Signing in to GitHub");
            if (_tools.Git == null) _tools.Detect(_s);
            var token = _ws.GitHubApiToken();
            if (token == null)
            {
                Log.Add(LogKind.Error, "No GitHub sign-in found. Sign in with GitHub Desktop, or paste a token in SETTINGS > Source (Actions: read & write).");
                return false;
            }
            var gh = new GitHubActions(_s.RemoteUrl, token);
            Step("Starting the Mac build for " + _s.Branch);
            long run = await gh.Start(_s.Branch, _s.DebugBuild, Log, ct);
            IosRunUrl = gh.RunUrl;
            Log.Add(LogKind.Command, "GitHub run: " + gh.RunUrl);
            string last = "";
            while (true)
            {
                await Task.Delay(10000, ct);
                GitHubActions.RunState st;
                try { st = await gh.Poll(run, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { Log.Add(LogKind.Warn, "poll: " + ex.Message); continue; }
                Step("Mac: " + st.Step, st.Total > 0 ? (float)st.Done / st.Total : -1);
                if (st.Step != last) { Log.Add(LogKind.Output, "mac> " + st.Step); last = st.Step; }
                if (st.Status != "completed") continue;
                if (st.Conclusion != "success")
                {
                    Log.Add(LogKind.Error, $"The Mac build {st.Conclusion}. Open the run for the log: {gh.RunUrl}");
                    return false;
                }
                break;
            }
            Step("Downloading the .ipa");
            var outDir = Path.Combine(_ws.Exists ? _ws.Dir : LauncherSettings.DataDir, "Builds", "iOS");
            var ipa = await gh.DownloadIpa(run, outDir, ct);
            LastArtifact = ipa;
            Log.Add(LogKind.Success, $"Built {ipa} - open it in Sideloadly to sign and install it on your iPhone.");
            return true;
        });

        public void BuildPhone(bool ios) => Start(ios ? "Build iOS" : "Build Android", async ct =>
        {
            if (!await EnsureTools(ct)) return false;
            // Unsaved edits in the workspace are what the user wants to try: build them as they are.
            int pending = _ws.HasEngine ? _ws.PendingChanges() : 0;
            if (pending > 0)
                Log.Add(LogKind.Warn, $"Using the workspace as it is: it has {pending} unsaved change{(pending == 1 ? "" : "s")}, so {_s.Branch} was not pulled. " +
                                      "Commit them to a branch or discard them on the GIT page to get the latest again.");
            else if (_s.PullBeforePlay || !_ws.HasEngine)
                if (!await SyncStep(ct)) return false;
            bool xcode = ios && _s.Ios == IosMode.Xcode;
            Step(xcode ? "Exporting the Xcode project" : ios ? "Building the iOS app" : "Building the Android app (first run installs the Android SDK)");
            var args = new List<string> { "run", "--project", _ws.BuildProject, "-c", "Release", "--", ios ? "ios" : "android" };
            if (xcode) args.Add("--xcode");
            if (!ios)
            {
                if (!string.IsNullOrWhiteSpace(_s.AndroidAbis)) { args.Add("--abi"); args.Add(_s.AndroidAbis.Trim()); }
                if (_s.AndroidBundle) args.Add("--aab");
                if (!string.IsNullOrWhiteSpace(_s.KeystorePath))
                {
                    args.Add("--keystore"); args.Add(_s.KeystorePath);
                    args.Add("--alias"); args.Add(_s.KeystoreAlias);
                }
            }
            if (_s.DebugBuild) args.Add("--debug");
            var r = await ProcessRunner.Run(_tools.Dotnet!, args, _ws.Dir, Log, ct, _tools.DotnetEnv(), PhoneProgress);
            if (r.ExitCode != 0) { Log.Add(LogKind.Error, (ios ? "iOS" : "Android") + " build failed - the messages above say why."); return false; }
            var outDir = Path.Combine(_ws.Dir, "Builds", ios ? "iOS" : "Android");
            var artifact = Directory.Exists(outDir)
                ? Directory.EnumerateFiles(outDir, ios ? "*.ipa" : "*.a?b", SearchOption.TopDirectoryOnly)
                    .Concat(xcode ? Directory.EnumerateDirectories(outDir, "*.xcodeproj") : Enumerable.Empty<string>())
                    .Concat(Directory.EnumerateFiles(outDir, "*.apk", SearchOption.TopDirectoryOnly))
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            LastArtifact = artifact ?? outDir;
            Log.Add(LogKind.Success, artifact != null ? $"Built {artifact}" : $"Output in {outDir}");
            return true;
        });

        // ---------------------------------------------------------------- steps

        async Task<bool> EnsureTools(CancellationToken ct)
        {
            Step("Checking tools");
            _tools.Detect(_s);
            if (_tools.Git == null)
            {
                Log.Add(LogKind.Error, "git was not found. Install GitHub Desktop (https://desktop.github.com) or Git for Windows, then press Refresh in Options.");
                return false;
            }
            if (_tools.Dotnet == null)
            {
                Log.Add(LogKind.Info, $".NET {Toolchain.RequiredDotnetMajor} SDK not found - installing a private copy for the launcher.");
                Step("Installing .NET SDK");
                if (!await _tools.InstallDotnet(Log, ct)) return false;
                _tools.Detect(_s);
                if (_tools.Dotnet == null) { Log.Add(LogKind.Error, ".NET was installed but could not be started."); return false; }
            }
            if (!_tools.VcRuntime)
                Log.Add(LogKind.Warn, "Microsoft Visual C++ Redistributable not found - if the game fails to start, install it from https://aka.ms/vs/17/release/vc_redist.x64.exe");
            return true;
        }

        async Task<bool> SyncStep(CancellationToken ct)
        {
            if (_tools.Git == null) _tools.Detect(_s);
            if (_tools.Git == null) { Log.Add(LogKind.Error, "git was not found."); return false; }
            Step("Syncing " + _s.Branch, 0);
            bool ok = await _ws.Sync(_s.Branch, Log, (p, what) => Step(what, p), ct);
            RefreshLocalState();
            if (ok && Commit != null) Log.Add(LogKind.Success, $"{_s.Branch} @ {Commit.Sha} - {Commit.Subject}");
            return ok;
        }

        int _projectsBuilt;
        async Task<bool> BuildPlayer(CancellationToken ct)
        {
            _projectsBuilt = 0;
            Step("Compiling the engine and the game", 0);
            string cfg = _s.ReleaseBuild ? "Release" : "Debug";
            var r = await ProcessRunner.Run(_tools.Dotnet!, new[] { "build", _ws.PlayerProject, "-c", cfg, "-nologo", "-v:minimal", "-clp:NoSummary" },
                _ws.Dir, Log, ct, _tools.DotnetEnv(), line =>
                {
                    // "Project -> path.dll" once per project; the player has ~8 in its graph.
                    if (line.Contains(" -> ")) { _projectsBuilt++; Step("Compiling the engine and the game", Math.Min(0.95f, _projectsBuilt / 8f)); }
                });
            if (r.ExitCode != 0) { Log.Add(LogKind.Error, "Build failed - the compiler messages above say why."); return false; }
            return true;
        }

        void PhoneProgress(string line)
        {
            var m = Regex.Match(line, @"^\[(\d+)/(\d+)\]");
            if (m.Success) Progress = float.Parse(m.Groups[1].Value) / float.Parse(m.Groups[2].Value);
        }

        public string PlayerExe =>
            Path.Combine(_ws.Dir, "Port", "src", "CosmicShore.Player", "bin", _s.ReleaseBuild ? "Release" : "Debug", "net10.0",
                OperatingSystem.IsWindows() ? "CosmicShore.exe" : "CosmicShore");

        bool LaunchGame(bool audio)
        {
            Step("Starting the game", 1);
            if (!File.Exists(PlayerExe)) { Log.Add(LogKind.Error, "The build finished but the player was not found at " + PlayerExe); return false; }
            var psi = new ProcessStartInfo(PlayerExe)
            {
                WorkingDirectory = Path.Combine(_ws.Dir, "Port"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var a in PlayerArgs()) psi.ArgumentList.Add(a);
            foreach (var kv in _tools.DotnetEnv()) psi.Environment[kv.Key] = kv.Value;
            psi.Environment["COSMIC_SHORE_PROJECT"] = _ws.Dir;
            if (!audio) psi.Environment["COSMIC_SHORE_AUDIO"] = "off";
            if (!_s.Network) psi.Environment["COSMIC_SHORE_NET"] = "off";
            if (_s.MobileRenderPath) psi.Environment["COSMIC_SHORE_GLES"] = "1";
            if (!string.IsNullOrWhiteSpace(_s.Profile)) psi.Environment["COSMIC_SHORE_PROFILE"] = _s.Profile.Trim();
            // Every play session leaves a report (scenes, frame times, errors) the CLAUDE page can hand to Claude.
            LastSessionReport = Path.Combine(SessionsDir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            psi.Environment["COSMIC_SHORE_SESSION_REPORT"] = LastSessionReport;
            psi.Environment["COSMIC_SHORE_BRANCH"] = _s.Branch;
            var commit = ProcessRunner.Capture(_tools.Git ?? "git", "-C", _ws.Dir, "rev-parse", "--short", "HEAD");
            if (commit != null) psi.Environment["COSMIC_SHORE_COMMIT"] = commit;
            PruneSessions();

            _game = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _game.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            _game.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            _game.Exited += (_, _) => { Log.Add(LogKind.Info, $"Game closed (exit code {SafeExit(_game)})."); GameExited?.Invoke(); };
            _game.Start();
            _game.BeginOutputReadLine();
            _game.BeginErrorReadLine();
            Log.Add(LogKind.Success, "Game running. Have fun.");
            return true;
        }

        /// <summary>Raised when a game started from PLAY closes (its session report is written by then, or a moment later).</summary>
        public event Action? GameExited;

        public static string SessionsDir => Path.Combine(LauncherSettings.DataDir, "sessions");
        public string? LastSessionReport { get; private set; }

        /// <summary>The newest session reports, newest first.</summary>
        public static List<FileInfo> Sessions() =>
            Directory.Exists(SessionsDir)
                ? new DirectoryInfo(SessionsDir).GetFiles("session-*.json").OrderByDescending(f => f.LastWriteTimeUtc).ToList()
                : new List<FileInfo>();

        static void PruneSessions()
        {
            try { foreach (var f in Sessions().Skip(40)) f.Delete(); } catch { }
        }

        public List<string> PlayerArgs()
        {
            var a = new List<string>();
            if (!string.IsNullOrWhiteSpace(_s.Resolution)) { a.Add("--size"); a.Add(_s.Resolution.Trim()); }
            if (_s.Fullscreen) a.Add("--fullscreen");
            if (!string.IsNullOrWhiteSpace(_s.StartScene)) { a.Add("--scene"); a.Add(_s.StartScene); }

            if (_s.VerboseLogs) a.Add("--verbose");
            foreach (var x in SplitArgs(_s.ExtraArgs)) a.Add(x);
            return a;
        }

        static int SafeExit(Process p) { try { return p.ExitCode; } catch { return -1; } }

        static IEnumerable<string> SplitArgs(string s)
        {
            foreach (Match m in Regex.Matches(s ?? "", "\"([^\"]*)\"|(\\S+)"))
                yield return m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        }
    }
}
