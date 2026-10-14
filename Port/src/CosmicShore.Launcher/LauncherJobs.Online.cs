using System;
using System.Diagnostics;
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

        /// <summary>The UGS environment every game process signs in to, when SETTINGS > ONLINE names one (else the branch's).</summary>
        void OnlineEnvironment(ProcessStartInfo psi)
        {
            if (!string.IsNullOrWhiteSpace(_s.UgsEnvironment)) psi.Environment["COSMIC_SHORE_UGS_ENVIRONMENT"] = _s.UgsEnvironment.Trim();
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
