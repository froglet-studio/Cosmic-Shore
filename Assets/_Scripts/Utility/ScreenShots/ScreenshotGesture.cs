using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// "Take a photograph." <b>P</b>, or the pad's <b>Select / View / Share</b> button.
    ///
    /// <para>One predicate in one place, the same shape as <c>OverviewGesture</c>, so the binding
    /// can never drift between the places that ask.</para>
    ///
    /// <para><b>P is not free on the keyboard, and this is the record of that.</b>
    /// <c>KeyboardInputStrategy</c> — the dual-WASD desktop scheme every two-stick hull flies on —
    /// reads <c>pKey</c> as the RIGHT STICK's vertical axis (WASD left, P/;/L/' right). So on
    /// those hulls a capture press also feeds one frame of stick to the vessel, and a HELD P keeps
    /// feeding it. The one-thumb hulls are unaffected: <c>SingleStickMouseInputStrategy</c> reads
    /// only the left stick, so P reaches nothing there. It is bound here anyway because it is what
    /// was asked for and the nudge is small; the clean fix is to move the keyboard scheme's
    /// right-stick-up off P, which is a player-facing rebind and therefore a separate decision.</para>
    ///
    /// <para>The other keys are all taken: F5/F6/F7 are the diagnostics HUD, F8 the manual sweep
    /// mark, F9 the benchmark overlays, F10 was this until it moved here, F11 fullscreen.
    /// <b>F12 is deliberately NOT used</b> — it is Steam's own screenshot key by default, so
    /// binding it would fire two captures, one of which has the UI in it. On the pad, Select is
    /// the only face/system button no vessel ability or menu gesture claims (Start is the
    /// overview).</para>
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
