using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Put me in my teammate's ship." The gesture that hands the pilot's hull to the AI and takes
    /// over an AI teammate's hull in an ARENA match (<see cref="PilotSwap"/>) - <b>D-pad left /
    /// right</b> on a pad, <b>1 / 2</b> on the keyboard. Left / 1 steps to the PREVIOUS teammate
    /// hull, right / 2 to the NEXT, round a ring every member of the team sees in the same order.
    ///
    /// <para>One predicate, in one place, for the same reason <see cref="RearViewGesture"/> is:
    /// polled only by <c>InputController</c>, which is already local-pilot gated and pause gated,
    /// so an AI hull, a remote replica, the overview and a modal cannot fire it. It is read here
    /// rather than routed through <c>InputEvents</c> on purpose: it moves the PILOT between hulls,
    /// it is not an ability, and putting it in a vessel's ability map would make it something a
    /// hull could fail to author.</para>
    ///
    /// <para><b>Why these controls.</b> The D-pad is unread in flight - no strategy binds it and no
    /// vessel ability map names it - which is what makes it a free shelf. On the keyboard the
    /// flight scheme eats WASD, P ; L ', QWER, Space, both Shifts and C; the number row is the
    /// shelf nothing flies with (the screenshot director took 0 for the same reason), and 1 / 2
    /// sit where the left hand already is. A swap is a deliberate, occasional act, so a key you
    /// cannot fat-finger mid-manoeuvre is the right trade.</para>
    /// </summary>
    public static class PilotSwapGesture
    {
        /// <summary>-1 (previous teammate), +1 (next), or 0 when the gesture did not fire this frame.</summary>
        public static int DirectionThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) return -1;
                if (keyboard.digit2Key.wasPressedThisFrame) return +1;
            }

            var pad = Gamepad.current;
            if (pad == null) return 0;

            if (pad.dpad.left.wasPressedThisFrame) return -1;
            if (pad.dpad.right.wasPressedThisFrame) return +1;
            return 0;
        }
    }
}
