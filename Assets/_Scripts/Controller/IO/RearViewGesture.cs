using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Look behind me." The gesture that flips the gameplay camera to the rear vantage and
    /// back — <b>C</b> on the keyboard, or <b>both shoulder buttons at once</b> (LB+RB) on a pad.
    ///
    /// <para>One predicate, in one place, for the same reason <see cref="OverviewGesture"/> is:
    /// the view a pilot gets is a property of the PLATFORM, not of a vessel or a mode, so the
    /// gesture has to mean the same thing everywhere and there must be exactly one thing to
    /// read when asking what it is bound to. It is polled by <c>InputController</c>, which is
    /// already local-pilot gated and pause gated — a remote replica, an AI hull and a paused
    /// game cannot fire it.</para>
    ///
    /// <para><b>The pad chord is deliberately STATELESS.</b> It fires on the single frame the
    /// chord completes: both shoulders down, and at least one of them going down THIS frame.
    /// That is an exact rising edge whichever button the player presses first, it cannot
    /// re-fire while the chord is held, and — unlike a remembered was-both-held-last-frame flag
    /// — it carries no state that could survive a scene load, a device swap, or an editor
    /// play-mode exit and desynchronise the toggle from what the player is holding.</para>
    ///
    /// <para><b>Known overlap, stated rather than papered over:</b> the right shoulder is not a
    /// free button. <c>GamepadInputStrategy</c> reads it as <c>Throttle</c> and raises
    /// <c>FlipAction</c> from it, so completing this chord also boosts and flips on any vessel
    /// bound to those. The left shoulder is genuinely unused (its handlers in that strategy are
    /// commented out), which is what makes LB the half that carries the intent. Both are read
    /// here rather than routed through <c>InputEvents</c> on purpose: this is a camera gesture,
    /// not a vessel ability, and putting it in the ability map would make it something a hull
    /// could fail to author.</para>
    /// </summary>
    public static class RearViewGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            if (pad == null) return false;

            // Both down, one of them newly down: the frame the chord closes, in either order.
            return pad.leftShoulder.isPressed && pad.rightShoulder.isPressed &&
                   (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame);
        }
    }
}
