using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// "Take a photograph." <b>P</b>, or the pad's <b>Select / View / Share</b> button.
    ///
    /// <para>One predicate in one place, the same shape as <c>OverviewGesture</c>, so the binding
    /// can never drift between the places that ask.</para>
    ///
    /// <para><b>P was NOT free, and the flight scheme moved rather than this key.</b>
    /// <c>KeyboardInputStrategy</c> — the dual-WASD desktop scheme every two-stick hull flies on —
    /// read <c>pKey</c> as the RIGHT STICK's vertical axis, so a capture press would also have fed
    /// the vessel a frame of stick and a HELD P would have kept feeding it: a nudge to the very
    /// framing this system exists to produce. That scheme's right-stick-up is now <b>O</b>, which
    /// sits in the same right-hand cluster and was used nowhere in the project. The rule it
    /// records: <b>a key is only "free" against the keys some OTHER system is reading, and an
    /// input scheme that consumes raw keys advertises none of them</b> — so check every
    /// <c>Keyboard.current</c> read before claiming one, and when two systems want one key, move
    /// the one whose binding is arbitrary.</para>
    ///
    /// <para>The keyboard flight scheme consumes <c>WASD</c>, <c>O</c>, <c>;</c>, <c>L</c>,
    /// <c>'</c>, <c>QWER</c>, Space and both Shifts. F5/F6/F7 are the diagnostics HUD, F8 the
    /// manual sweep mark, F9 the benchmark overlays, F10 was this until it moved here, F11
    /// fullscreen. <b>F12 is deliberately NOT used</b> — it is Steam's own screenshot key by
    /// default, so binding it would fire two captures, one of which has the UI in it. On the pad,
    /// Select is the only face/system button no vessel ability or menu gesture claims (Start is
    /// the overview).</para>
    /// </summary>
    public static class ScreenshotGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
        }
    }
}
