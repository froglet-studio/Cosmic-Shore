using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Look behind me." <b>HELD</b>, not toggled — the rear view lasts exactly as long as
    /// <b>C</b> is down on the keyboard, or <b>both shoulder buttons at once</b> (LB+RB) on a
    /// pad. Let go and you are facing forward again.
    ///
    /// <para><b>Held is the right shape for a glance, and a toggle is not.</b> A look-back is
    /// something a pilot does for half a second in the middle of flying forward; a toggle makes
    /// the dangerous state (flying at speed while looking backwards) the one you can walk away
    /// from and forget you are in, and it needs a second deliberate press to escape. Holding
    /// also cannot desynchronise: there is no remembered state to disagree with the button, so
    /// a dropped frame, a scene load, a device swap or a pause can never leave a pilot stuck
    /// facing the wrong way. It is the same reason a car's mirror is a glance rather than a
    /// mode.</para>
    ///
    /// <para>One predicate, in one place, in the shape of <see cref="OverviewGesture"/>: the
    /// binding must mean the same thing everywhere and there must be exactly one thing to read
    /// when asking what it is. It is polled by <c>InputController</c>, which is already
    /// local-pilot gated and pause gated — a remote replica, an AI hull and a paused game cannot
    /// drive it. Whether a given vessel HAS a rear view is a separate question, answered by that
    /// hull's <c>CameraSettingsSO.enableRearView</c>; this only reports what the player is
    /// holding.</para>
    ///
    /// <para><b>Known overlap, stated rather than papered over:</b> the right shoulder is not a
    /// free button. <c>GamepadInputStrategy</c> reads it as <c>Throttle</c> and raises
    /// <c>FlipAction</c> from it, so holding this chord also holds the boost on any vessel bound
    /// to those. The left shoulder is genuinely unused (its handlers in that strategy are
    /// commented out), which is what makes LB the half that carries the intent. Both are read
    /// here rather than routed through <c>InputEvents</c> on purpose: this is a camera gesture,
    /// not a vessel ability, and putting it in the ability map would make it something a hull
    /// could fail to author.</para>
    /// </summary>
    public static class RearViewGesture
    {
        /// <summary>
        /// True for every frame the player is asking to look behind them. Deliberately a level,
        /// not an edge: the caller re-reports it each frame and the rear view follows, so
        /// nothing anywhere has to remember that a glance is in progress.
        /// </summary>
        public static bool IsHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.isPressed) return true;

            var pad = Gamepad.current;
            return pad != null && pad.leftShoulder.isPressed && pad.rightShoulder.isPressed;
        }
    }
}
