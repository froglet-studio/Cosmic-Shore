using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Fail-loud reporting for the one-thumb mouse scheme
    /// (<see cref="SingleStickMouseInputStrategy"/>).
    ///
    /// <para>Its failure mode is SILENCE. Every reason `InputController.UseSingleStickMouse` can
    /// decline leaves the player on <see cref="KeyboardInputStrategy"/>, which still steers a
    /// one-thumb hull off WASD and still fires every ability off the same keys — so the scheme
    /// not engaging is indistinguishable from the scheme being broken, and the first playtest
    /// report was exactly that: <i>"I found keys that used my abilities, but the mouse did not
    /// fly the vessel."</i> Nothing in the console said which of five things had happened.</para>
    ///
    /// <para>So it says so, ONCE per reason for the lifetime of the process, in the shape
    /// <c>PrismOcclusionDiagnostics</c> and <c>VesselVisionDiagnostics</c> already use for a
    /// system that can silently fail to engage. Legitimate states are silent: a pad is connected
    /// (the pad wins and should), the device is handheld, dual mouse is engaged, or the vessel is
    /// simply a two-stick hull that this scheme cannot serve — none of those reach here, because
    /// `SelectStrategy` returns before asking.</para>
    ///
    /// <para>It also reports the transition INTO mouse flight once, because "did it engage at
    /// all" is the first question a playtest asks and the cursor lock is the only other evidence.</para>
    /// </summary>
    public static class MouseFlightDiagnostics
    {
        public enum Reason
        {
            /// <summary>No mouse device. Nothing to fly with; not a fault.</summary>
            NoMouse = 0,
            /// <summary>An AI or a remote replica's InputController. Correct, and expected.</summary>
            NotLocalPilot = 1,
            /// <summary>The player has no vessel yet, or it has been despawned.</summary>
            NoVessel = 2,
            /// <summary>The vessel is flying itself.</summary>
            Autopilot = 3,
            /// <summary>A two-stick hull. The mouse cannot serve one — see the strategy's doc.</summary>
            NotSingleStick = 4,
            /// <summary>
            /// A gamepad currently holds the input family on a ONE-THUMB hull. Legitimate while the
            /// player is actually using the pad — and the shape of this scheme's longest-running
            /// bug when they are not: with mouse MOTION excluded from actuation, a connected pad
            /// took the ship at startup and any stick drift over 0.25 took it back after every
            /// click, so the cursor locked, the mouse buttons fired abilities, and the vessel never
            /// turned. Moving the mouse now wins it back within 0.08 s; if this keeps printing
            /// while nobody is touching the pad, that pad is actuating on its own.
            /// </summary>
            GamepadOwnsInput = 5,
        }

        static bool _engagedReported;
        static readonly bool[] _reported = new bool[6];

        // Domain reload can be disabled, which would otherwise latch every flag from the previous
        // play session and make the whole diagnostic silent exactly when it is being iterated on.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _engagedReported = false;
            for (int i = 0; i < _reported.Length; i++) _reported[i] = false;
        }

        /// <summary>
        /// Report why mouse flight is not engaging, once per reason, and return false so the
        /// caller reads as a plain guard: <c>return MouseFlightDiagnostics.Decline(reason);</c>
        /// </summary>
        public static bool Decline(Reason reason, string detail = null)
        {
            int index = (int)reason;
            if (index >= 0 && index < _reported.Length && !_reported[index])
            {
                _reported[index] = true;
                CSDebug.LogWarning($"[MouseFlight] One-thumb mouse controls are NOT engaged: " +
                                   $"{Explain(reason, detail)} Flight stays on the dual-WASD " +
                                   $"keyboard scheme. See _Scripts/Controller/IO/ONE_THUMB_MOUSE_CONTROLS.md.");
            }
            return false;
        }

        /// <summary>
        /// Report the first frame mouse flight takes over — on a CHANNEL, off by default, because
        /// this is bring-up telemetry for a finished system rather than a fault. The refusals
        /// above stay unconditional warnings: a system whose failure mode is silence has to be
        /// loud when it fails, and quiet when it works.
        /// </summary>
        public static void Engaged()
        {
            if (_engagedReported) return;
            _engagedReported = true;
            CSDebug.LogVerbose(CSLogChannel.MouseFlight,
                "[MouseFlight] One-thumb mouse controls engaged — the mouse is now the vessel's " +
                "single stick and the cursor is locked.");
        }

        static string Explain(Reason reason, string detail) => reason switch
        {
            Reason.NoMouse => "no mouse device is present.",
            Reason.NotLocalPilot =>
                "this InputController belongs to an AI or a remote player, not the local pilot. " +
                "Expected — but if it is YOUR ship, IPlayer.IsLocalPilot is answering false.",
            Reason.NoVessel =>
                "the player has no vessel yet (or it was despawned). If this is the only message " +
                "you see, the player-vessel pair never resolved on this machine.",
            Reason.Autopilot =>
                "the vessel is on autopilot. In Menu_Main that is the lava lamp and is correct; " +
                "enter freestyle to fly it yourself.",
            Reason.NotSingleStick =>
                $"the vessel{(string.IsNullOrEmpty(detail) ? string.Empty : $" ({detail})")} is a " +
                "TWO-stick hull — IsSingleStickControls is false, so it reads the dual-stick mix " +
                "the mouse cannot drive without cross-talk. If this is a Sparrow, Serpent, " +
                "Grizzly, Termite, Falcon, Shrike or Scarab, its VesselTransformer is not the " +
                "SingleStick/Scarab one, or Initialize never ran on it.",
            _ => "unknown reason.",
        };
    }
}
