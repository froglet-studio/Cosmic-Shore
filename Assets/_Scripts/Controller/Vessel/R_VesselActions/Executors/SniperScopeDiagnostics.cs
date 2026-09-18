using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fail-loud reporting for the Serpent scope's on-screen instrument
    /// (<see cref="SniperScopeOverlay"/>).
    ///
    /// <para><b>Its failure mode is a BLANK SCREEN, and a blank screen is one report for three
    /// completely different faults.</b> Since round 4 retired the main-camera cockpit, the
    /// eyepiece is the ability's ONLY visible output — so if it does not appear, the pilot cannot
    /// tell whether the scope engaged at all, whether the window ticked and drew nothing, or
    /// whether it drew correctly underneath something opaque. Two playtests in a row reported the
    /// same four words ("the pip was not there") for what may not have been the same defect, and
    /// nothing in the console distinguished them.</para>
    ///
    /// <para>So it says so, ONCE per reason for the lifetime of the process, in the shape
    /// <c>MouseFlightDiagnostics</c>, <c>PrismOcclusionDiagnostics</c> and
    /// <c>VesselVisionDiagnostics</c> already use. The split is the house one: a REFUSAL is an
    /// unconditional warning naming the gate, because a system whose failure mode is silence has
    /// to be loud when it fails; the happy path is one line on <c>CSLogChannel.SerpentScope</c>,
    /// off by default, because bring-up telemetry for a working system is console spam.</para>
    ///
    /// <para><b>There is deliberately no rung for "it is being covered".</b> When every gate and
    /// every self-check passes, the remaining question is *what is drawn over it* — which no
    /// static reading of a prefab or a scene can answer, only a rendered frame. An unconditional
    /// warning saying so would be a permanent false positive the day the real defect is fixed, so
    /// the guidance rides the happy-path line instead (<see cref="Drawing"/>) and names the tool:
    /// FrogletTools ▸ Diagnostics ▸ Report On-Screen UI, in play mode. Enabling this one channel
    /// is therefore the whole procedure for separating "never ticked" from "covered".</para>
    /// </summary>
    public static class SniperScopeDiagnostics
    {
        public enum Reason
        {
            /// <summary>No <c>IVesselStatus</c>, or no <c>Player</c> on it yet.</summary>
            NoPlayer = 0,

            /// <summary>An AI or a remote replica. Correct and expected — a screen is a thing one
            /// machine has, so only the local pilot builds a window.</summary>
            NotLocalPilot = 1,

            /// <summary>
            /// The sniper shot executor did not resolve from the registry. The scope cannot draw
            /// its reticle (which IS the shot's cone) or its recharge ring without it.
            /// </summary>
            NoShotExecutor = 2,

            /// <summary>
            /// Neither <c>IVesselStatus.Vessel.Transform</c> nor this executor's own root resolved
            /// a hull to put the scope's eye in front of. Should be unreachable.
            /// </summary>
            NoHull = 3,

            /// <summary>
            /// The instrument ticked, but something about its own geometry or canvas would make it
            /// invisible. The detail names which.
            /// </summary>
            Unusable = 4,
        }

        static bool _drawingReported;
        static readonly bool[] _reported = new bool[5];

        // Domain reload can be disabled, which would otherwise latch every flag from the previous
        // play session and make the whole diagnostic silent exactly when it is being iterated on.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _drawingReported = false;
            for (int i = 0; i < _reported.Length; i++) _reported[i] = false;
        }

        /// <summary>
        /// Report why the scope's window is not being drawn, once per reason, and return false so
        /// the caller reads as a plain guard:
        /// <c>if (x == null) { SniperScopeDiagnostics.Decline(Reason.NoHull); return; }</c>
        /// </summary>
        public static bool Decline(Reason reason, string detail = null)
        {
            int index = (int)reason;
            if (index >= 0 && index < _reported.Length && !_reported[index])
            {
                _reported[index] = true;
                CSDebug.LogWarning($"[SerpentScope] The scope window is NOT being drawn: " +
                                   $"{Explain(reason, detail)} See " +
                                   $"_Scripts/Controller/Vessel/R_VesselActions/SERPENT_SNIPER_SCOPE.md.");
            }
            return false;
        }

        /// <summary>
        /// Report a window that IS ticking but whose own state would make it invisible. Always a
        /// warning — this is the case a pilot reports as "it is not there" while every gate above
        /// passed, so it must never be behind a channel.
        /// </summary>
        public static void Unusable(string detail) => Decline(Reason.Unusable, detail);

        /// <summary>
        /// Report the first frame the instrument lays out — on a CHANNEL, off by default, because
        /// this is bring-up telemetry for a working system rather than a fault. The refusals above
        /// stay unconditional warnings.
        /// </summary>
        public static void Drawing(Vector2 centre, float radius, Vector2 screen)
        {
            if (_drawingReported) return;
            _drawingReported = true;
            CSDebug.LogVerbose(CSLogChannel.SerpentScope,
                $"[SerpentScope] Eyepiece laid out: centre {centre} radius {radius:0.#} px on a " +
                $"{screen.x:0}x{screen.y:0} screen. If nothing is visible there, the window is " +
                $"being covered — run FrogletTools > Diagnostics > Report On-Screen UI in play mode.");
        }

        static string Explain(Reason reason, string detail) => reason switch
        {
            Reason.NoPlayer =>
                "this vessel has no Player yet. If the scope is being held and this is the only " +
                "message, the player-vessel pair never resolved on this machine.",
            Reason.NotLocalPilot =>
                "this vessel is an AI or a remote replica. Expected — but if it is YOUR ship, " +
                "IPlayer.IsLocalPilot is answering false, and no HUD of any kind will draw.",
            Reason.NoShotExecutor =>
                "SniperShotActionExecutor did not resolve from the ActionExecutorRegistry, so the " +
                "reticle has no cone to measure and the ring has no recharge to draw. Check the " +
                "vessel prefab carries that component under its ShipActions container.",
            Reason.NoHull =>
                "no hull transform resolved — IVesselStatus.Vessel is null AND this executor has " +
                "no root, which should be impossible. The scope's eye has nowhere to sit.",
            Reason.Unusable =>
                $"the instrument ticked but cannot be seen: {detail}",
            _ => "unknown reason.",
        };
    }
}
