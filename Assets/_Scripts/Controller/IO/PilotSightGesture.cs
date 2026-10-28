using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Show me what my pilot sees." HELD, not toggled: the sight is open for exactly as long as
    /// <b>LB</b> (pad left shoulder) or <b>V</b> (keyboard) is held (Docs/Pilots/CALLSIGNS.md §5).
    ///
    /// <para>Read here, the way <see cref="RearViewGesture"/> is, rather than through
    /// <c>InputEvents</c>: the sight touches nothing but photons, so it is a property of the
    /// platform and not an ability a hull could fail to author. <see cref="PilotSight"/> polls it
    /// for the local pilot only, and only while that pilot's input is live.</para>
    ///
    /// <para><b>V on the keyboard</b> is a proposal: the cluster keys are taken (Space T Q F E, C the
    /// look-back), and V sits under the resting left hand. The phone's gesture is still to come
    /// (the per-hull touch profiles, <c>Docs/claude/ARCHITECTURE_CORE.md</c>).</para>
    /// </summary>
    public static class PilotSightGesture
    {
        public static bool Held()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.isPressed) return true;

            var pad = Gamepad.current;
            return pad != null && pad.leftShoulder.isPressed;
        }
    }
}
