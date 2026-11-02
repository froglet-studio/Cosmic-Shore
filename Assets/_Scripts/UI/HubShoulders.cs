using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Who has LB / RB while a hub window is open. <see cref="ScreenSwitcher"/> slides a hub window to its neighbour on
    /// LB / RB (Shombith, 2026-10-25); a window with tabs of its own claims the bumpers while it is open, and the switcher
    /// leaves them to it (Shombith, 2026-11-02, the Voyage window's Voyages | Saga | Quests | Episodes "with LB and RB
    /// support"). LT / RT keep paging the top bar either way.
    /// </summary>
    public static class HubShoulders
    {
        static Object s_owner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_owner = null;

        /// <summary>True while an open window steps its own tabs on LB / RB.</summary>
        public static bool Claimed => s_owner;

        /// <summary>The bumpers are <paramref name="owner"/>'s until it releases them.</summary>
        public static void Claim(Object owner) => s_owner = owner;

        /// <summary>Give the bumpers back to the switcher, if <paramref name="owner"/> holds them.</summary>
        public static void Release(Object owner)
        {
            if (s_owner == owner) s_owner = null;
        }
    }
}
