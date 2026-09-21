using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// "Take a photograph." <b>F10</b>, or the pad's <b>Select / View / Share</b> button.
    ///
    /// <para>One predicate in one place, the same shape as <c>OverviewGesture</c>, so the binding
    /// can never drift between the places that ask.</para>
    ///
    /// <para>The keys are chosen by what is already taken: F5/F6/F7 are the diagnostics HUD, F8 the
    /// manual sweep mark, F9 the benchmark overlays, F11 fullscreen. <b>F12 is deliberately NOT
    /// used</b> — it is Steam's own screenshot key by default, so binding it would fire two
    /// captures, one of which has the UI in it. On the pad, Select is the only face/system button
    /// no vessel ability or menu gesture claims (Start is the overview).</para>
    /// </summary>
    public static class ScreenshotGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            return pad != null && pad.selectButton.wasPressedThisFrame;
        }
    }
}
