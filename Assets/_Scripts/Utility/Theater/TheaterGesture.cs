using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The theater's two bindings, in one place so they cannot drift between the places that ask.
    ///
    /// <para><b>9</b> starts and stops a recording. <b>8</b> plays the most recent recording back.</para>
    ///
    /// <para>The number row for the same reason <c>ScreenshotGesture</c> gives: every letter is
    /// spoken for by the dual-WASD flight scheme (<c>WASD</c>, <c>P</c>/<c>;</c>/<c>L</c>/<c>'</c>,
    /// <c>QWER</c> + Space, both Shifts) and the F-keys are spoken for by the diagnostics HUD
    /// (F5-F8), the benchmark overlays (F9), fullscreen (F11) and Steam (F12). <c>0</c> is the
    /// screenshot director; <b>1-9 are read by nothing else in <c>_Scripts</c></b>. Recording is a
    /// deliberate, occasional act, so a key away from the hands' resting clusters is one you
    /// cannot fat-finger mid-manoeuvre — and it leaves the flight scheme a recording depends on
    /// completely untouched.</para>
    ///
    /// <para>No pad binding: Select is the screenshot director's and Start is the overview, and a
    /// recording is started from the desk rather than from the stick.</para>
    /// </summary>
    public static class TheaterGesture
    {
        /// <summary>Start, or stop and write, a recording.</summary>
        public static bool ToggleRecordRequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.digit9Key.wasPressedThisFrame;
        }

        /// <summary>Enter the recording area with the most recent recording, or leave it.</summary>
        public static bool TogglePlaybackRequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.digit8Key.wasPressedThisFrame;
        }

        /// <summary>
        /// The transport a director holds a PAD for, live only while the recording area is up.
        ///
        /// <para>It is the D-pad and the south face button and nothing else, because the two sticks
        /// and both triggers belong to the free camera and a theater you can fly but not scrub is
        /// half a tool. These are safe to read unconditionally in the theater for the same reason
        /// the free camera is: <see cref="TheaterStage"/> has paused the local pilot's input, so
        /// nothing else on the machine is listening to the pad.</para>
        /// </summary>
        /// <summary>
        /// Pick a shot outright by number (0-3, in <c>TheaterShot</c>'s own order), or -1 for no
        /// request. Live only while the recording area is up.
        ///
        /// <para>The number row rather than the on-screen buttons, because a shot you can only
        /// reach by clicking is one you cannot reach while flying the free camera with a pad in
        /// both hands — and because the first playtest could not change shot at all: the buttons
        /// were being pressed THROUGH to the HUD underneath. A key cannot be covered.</para>
        /// </summary>
        public static int ShotRequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return -1;
            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>Watch the next pilot (Chase and Static follow one). Live only in the theater.</summary>
        public static bool NextPilotRequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.digit5Key.wasPressedThisFrame;
        }

        public static void ReadPadTransport(out int shotStep, out int pilotStep, out bool togglePause)
        {
            shotStep = 0;
            pilotStep = 0;
            togglePause = false;

            var pad = Gamepad.current;
            if (pad == null) return;

            if (pad.dpad.right.wasPressedThisFrame) shotStep += 1;
            if (pad.dpad.left.wasPressedThisFrame) shotStep -= 1;
            if (pad.dpad.up.wasPressedThisFrame) pilotStep += 1;
            if (pad.dpad.down.wasPressedThisFrame) pilotStep -= 1;
            togglePause = pad.buttonSouth.wasPressedThisFrame;
        }
    }
}
