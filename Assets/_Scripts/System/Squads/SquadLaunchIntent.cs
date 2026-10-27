using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// The squad the player is taking into a match (the Ark's Squads tab, TAKE INTO Mission / Arena), for this menu visit.
    /// The launch panel reads it to open the player on the squad's domain (<see cref="SquadMembership.TryLaunchDomain"/>,
    /// Docs/MENU_REDESIGN_B.md section 94). Runtime only, never saved: it is set by Take into and cleared when the player
    /// opens a window from Home directly (<c>MenuHubButton</c>), so a later plain Arena trip keeps the card's own memory.
    /// </summary>
    public static class SquadLaunchIntent
    {
        static SavedSquad _squad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _squad = null;

        /// <summary>The squad going in, or null.</summary>
        public static SavedSquad Squad => _squad;

        /// <summary>Take into pressed with <paramref name="squad"/>. A copy is kept: later edits in the Ark do not follow it in.</summary>
        public static void Set(SavedSquad squad) => _squad = SquadMembership.Copy(squad);

        public static void Clear() => _squad = null;
    }
}
