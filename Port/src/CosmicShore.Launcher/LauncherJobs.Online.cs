using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Online play: START ONLINE runs the game signed in to UGS, its parties and matches going through UGS Lobby and
    /// UGS Relay as the Unity build's do (docs/MULTIPLAYER.md §6.10). START plays offline. There is no server to
    /// host or find: UGS is always there.
    /// </summary>
    public sealed partial class LauncherJobs
    {
        /// <summary>Whether the game PLAY started last (or is starting) is online.</summary>
        public bool PlayingOnline { get; private set; }

        Process? _secondPlayer;

        /// <summary>Whether the second online player PLAY > + PLAYER 2 started is still running.</summary>
        public bool SecondPlayerRunning => ProcessRunner.IsRunning(_secondPlayer);

        /// <summary>
        /// PLAY > + PLAYER 2: while an online game runs, a second online game on this PC as another UGS player
        /// (its own profile and save, so its own sign-in), to party up with through UGS. The same build START ONLINE
        /// made, a smaller window beside the first, no sound.
        /// </summary>
        public void StartSecondPlayer()
        {
            if (SecondPlayerRunning) return;
            if (!File.Exists(PlayerExe)) { Log.Add(LogKind.Warn, "Press START ONLINE first: player 2 runs the build it makes."); return; }
            string profile = string.Equals(_s.Profile?.Trim(), "player2", StringComparison.OrdinalIgnoreCase) ? "player3" : "player2";
            var psi = PlayerStart(PlayerExe, new[] { "--size", "1280x720", "--position", "640,80" }, audio: false, network: true, profile: profile);
            NetLogEnvironment(psi, "p2");
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, "[P2] " + e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, "[P2] " + e.Data); };
            p.Exited += (_, _) => { Log.Add(LogKind.Info, "Player 2 closed."); SecondPlayerExited?.Invoke(); };
            p.Start();
            _secondPlayer = p;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            Log.Add(LogKind.Success, $"Player 2 starting online as the '{profile}' profile (its own UGS player). Its lines start with [P2].");
        }

        /// <summary>Raised when player 2 closes (its multiplayer test log is written by then).</summary>
        public event Action? SecondPlayerExited;

        /// <summary>Closes player 2's window first, so it writes its multiplayer test log as a close does; stops it if it does not close.</summary>
        public void StopSecondPlayer()
        {
            var p = _secondPlayer;
            if (!SecondPlayerRunning) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (p!.CloseMainWindow() && p.WaitForExit(5000)) return;
                    p.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
            });
        }

        /// <summary>Where online games write their multiplayer test logs (<see cref="Amoebius.NetTestLogFormat"/>); SEND TO CLAUDE sends them.</summary>
        public static string NetLogsDir => Path.Combine(LauncherSettings.DataDir, "netlogs");

        /// <summary>An online game writes its multiplayer test log when it closes, named for <paramref name="peer"/> ("main", "p2").</summary>
        void NetLogEnvironment(ProcessStartInfo psi, string peer)
        {
            psi.Environment["COSMIC_SHORE_NET_LOG_DIR"] = NetLogsDir;
            psi.Environment["COSMIC_SHORE_NET_PEER"] = peer;
            if (!psi.Environment.ContainsKey("COSMIC_SHORE_COMMIT") &&
                ProcessRunner.Capture(_tools.Git ?? "git", "-C", _ws.Dir, "rev-parse", "--short", "HEAD") is { } commit)
                psi.Environment["COSMIC_SHORE_COMMIT"] = commit;
        }

        /// <summary>What every game process gets from SETTINGS: the UGS environment (when one is named) and the diagnostics overlay.</summary>
        void OnlineEnvironment(ProcessStartInfo psi)
        {
            if (!string.IsNullOrWhiteSpace(_s.UgsEnvironment)) psi.Environment["COSMIC_SHORE_UGS_ENVIRONMENT"] = _s.UgsEnvironment.Trim();
            if (_s.Diagnostics) psi.Environment["COSMIC_SHORE_DIAG"] = "1";
        }

        /// <summary>
        /// SETTINGS > ONLINE > UGS CHECK: <c>CosmicShore --ugs-relay-check</c> (docs/MULTIPLAYER.md §6.8). Signs in two UGS
        /// players in the game's live project (the same two every time: their session tokens stay in the
        /// "relaycheck" profile), allocates, joins by code, connects through UGS Relay and times frames both ways.
        /// </summary>
        public void RunUgsRelayCheck() => Start("UGS relay check", async ct =>
        {
            if (!await EnsureTools(ct)) return false;
            string cfg = _s.ReleaseBuild ? "Release" : "Debug";
            if (!await EnsurePlayerBuilt(ct, cfg)) return false;
            Step("Signing in to UGS and going through UGS Relay", 1);
            var p = new Process { StartInfo = PlayerStart(PlayerExeFor(cfg), new[] { "--ugs-relay-check" }, audio: false, network: true, profile: "relaycheck"), EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) Log.Add(LogKind.Output, e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            using (ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }))
                await p.WaitForExitAsync(CancellationToken.None);
            bool ok = p.ExitCode == 0;
            Log.Add(ok ? LogKind.Success : LogKind.Error, ok ? "UGS Relay works from Amoebius." : "The UGS relay check failed; the [relay-check] lines above name the step.");
            return ok;
        });
    }
}
