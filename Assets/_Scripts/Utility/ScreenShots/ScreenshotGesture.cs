using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// "Take a photograph." <b>0</b> (the number row's zero), or the pad's
    /// <b>Select / View / Share</b> button.
    ///
    /// <para>One predicate in one place, the same shape as <c>OverviewGesture</c>, so the binding
    /// can never drift between the places that ask.</para>
    ///
    /// <para><b>Why a digit, when every other binding in the game is a letter or an F-key.</b>
    /// The letters are gone: <c>KeyboardInputStrategy</c> — the dual-WASD desktop scheme every
    /// two-stick hull flies on — consumes <c>WASD</c> (left stick), <c>P</c>/<c>;</c>/<c>L</c>/<c>'</c>
    /// (right stick), <c>QWER</c> + Space (the ability keys, shared verbatim with
    /// <c>SingleStickMouseInputStrategy</c>) and both Shifts (the triggers). <b>P in particular is
    /// right-stick-UP</b>, so binding capture there would have fed the vessel a frame of stick on
    /// every press and kept feeding it on a hold — a nudge to the very framing this system exists
    /// to produce. The F-keys are gone too: F5/F6/F7 are the diagnostics HUD, F8 the manual sweep
    /// mark, F9 the benchmark overlays, F10 was this until it moved here, F11 fullscreen, and
    /// <b>F12 is Steam's own screenshot key</b> — binding it fires two captures, one with the UI
    /// in it.</para>
    ///
    /// <para>The number row is not a compromise here, it is the right shelf: a capture is a
    /// deliberate, occasional act rather than a flight control, so a key <i>away</i> from the
    /// hands' resting clusters is one you cannot fat-finger mid-manoeuvre — and it leaves the
    /// flight scheme untouched, which is what a photograph of that flight needs. The digits are
    /// entirely unclaimed: <c>0</c>-<c>9</c> are read by nothing in <c>_Scripts</c>. On the pad,
    /// Select is the only face/system button no vessel ability or menu gesture claims (Start is
    /// the overview).</para>
    ///
    /// <para>General rule this records: <b>a key is only "free" against the keys some OTHER
    /// system is reading, and an input scheme that consumes raw <c>Keyboard.current</c> reads
    /// advertises none of them</b> — grep the reads before claiming one.</para>
    /// </summary>
    public static class ScreenshotGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.digit0Key.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
        }
    }
}
