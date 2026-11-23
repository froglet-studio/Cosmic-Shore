using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Look behind me." The gesture that flips the gameplay camera to the rear vantage and
    /// back — <b>C</b> on the keyboard, or <b>D-pad down</b> on a pad.
    ///
    /// <para>One predicate, in one place, for the same reason <see cref="OverviewGesture"/> is:
    /// the view a pilot gets is a property of the PLATFORM, not of a vessel or a mode, so the
    /// gesture has to mean the same thing everywhere and there must be exactly one thing to
    /// read when asking what it is bound to. It is polled by <c>InputController</c>, which is
    /// already local-pilot gated and pause gated — a remote replica, an AI hull and a paused
    /// game cannot fire it.</para>
    ///
    /// <para><b>It fires on the press EDGE and carries no state.</b> <c>wasPressedThisFrame</c>
    /// cannot re-fire while the button is held, and a remembered was-held-last-frame flag could
    /// survive a scene load, a device swap or an editor play-mode exit and desynchronise the
    /// toggle from what the player is holding.</para>
    ///
    /// <para><b>Why D-pad down</b> (Garrett, 2026-10-27). It was LB + RB together until the
    /// shoulders were given to the pilot: LB opens the pilot's vision and RB grabs the payload
    /// (<c>Docs/Pilots/CALLSIGNS.md</c>), so a shoulder chord would fire all three at once. D-pad
    /// down is free in flight: left and right are <see cref="PilotSwapGesture"/>'s, and the
    /// theater's D-pad transport (<c>TheaterGesture</c>) is only read while the local pilot's
    /// input is paused, which is exactly when this gesture is not polled.</para>
    ///
    /// <para>Read here rather than routed through <c>InputEvents</c> on purpose: this is a camera
    /// gesture, not a vessel ability, and putting it in the ability map would make it something
    /// a hull could fail to author.</para>
    /// </summary>
    public static class RearViewGesture
    {
        public static bool RequestedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame) return true;

            var pad = Gamepad.current;
            if (pad == null) return false;

            return pad.dpad.down.wasPressedThisFrame;
        }
    }
}
