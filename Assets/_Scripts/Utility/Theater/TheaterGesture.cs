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

        /// <summary>Play the most recent recording in the output folder, or stop one that is playing.</summary>
        public static bool TogglePlaybackRequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.digit8Key.wasPressedThisFrame;
        }
    }
}
