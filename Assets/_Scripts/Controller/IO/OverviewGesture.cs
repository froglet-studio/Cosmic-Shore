using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Take me to the overview." The keyboard and pad counterpart of the on-screen
    /// <b>Volume / Pause</b> button — <b>Escape</b>, or the pad's <b>Start</b>.
    ///
    /// <para>One predicate, asked by both HUDs (<c>MiniGameHUD</c> in a game scene,
    /// <c>MenuMiniGameHUD</c> in Menu_Main freestyle), because the two must not drift: the whole
    /// point is that the gesture means the same thing everywhere. Each HUD answers it by invoking
    /// its OWN volume/pause button, so Escape is not a parallel implementation of the overview —
    /// it IS the button, and whatever that button is authored to do is what the key does.</para>
    ///
    /// <para>This is also why a mouse pilot never needs the cursor handed back mid-flight: the one
    /// control they would have reached for it is on this key. The cursor is released when the
    /// overview actually opens (the input controller pauses, and the strategy unlocks it there),
    /// not speculatively before.</para>
    /// </summary>
    public static class OverviewGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }
    }
}
