using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The card a match was launched from, carried through the game scene so that leaving the match
    /// lands back on that card's launch panel instead of on HOME - the result panel's Main Menu, the
    /// pause menu's, the Maelstrom summary's. The panel re-seeds itself from
    /// <see cref="Core.LaunchPreferenceStore"/>, so the player is back on the setup they just flew and
    /// can change the AI difficulty or the intensity and go again (Yash, 2026-10-25: "this should be
    /// the general flow"). <see cref="ScreenSwitcher"/> does the re-opening; the launch panel
    /// (<c>ArcadeGameConfigureModal</c>) writes the record.
    ///
    /// <para><b>In memory only, on purpose.</b> A scene load keeps a static and an application
    /// restart does not, so a player who quit the app mid-match still opens on HOME. It is TAKEN by
    /// the first menu load after the launch, so a later reload that is not a return (a party leave
    /// from the menu, a sign-out) finds nothing.</para>
    /// </summary>
    public static class MenuReturnCard
    {
        /// <summary>What a return re-opens: the card's mode, and whether it was this week's challenge.</summary>
        public readonly struct Card
        {
            public readonly GameModes Mode;
            public readonly bool WeeklyChallenge;

            public Card(GameModes mode, bool weeklyChallenge)
            {
                Mode = mode;
                WeeklyChallenge = weeklyChallenge;
            }
        }

        static bool s_armed;
        static Card s_card;

        /// <summary>True while a launched card is waiting for the menu to come back to it.</summary>
        public static bool IsArmed => s_armed;

        /// <summary>Record the card a launch is about to fly. Every peer writes it, host and guest alike.</summary>
        public static void Remember(GameModes mode, bool weeklyChallenge)
        {
            s_card = new Card(mode, weeklyChallenge);
            s_armed = true;
        }

        /// <summary>
        /// Forget it: the menu this session lands on is not a return from that match (the party was
        /// lost and the player is bounced to a solo menu with a notice).
        /// </summary>
        public static void Clear() => s_armed = false;

        /// <summary>Hand the card to the menu that is loading, once. False when no launch is waiting.</summary>
        public static bool TryTake(out Card card)
        {
            card = s_card;
            if (!s_armed) return false;

            s_armed = false;
            return true;
        }

        /// <summary>
        /// Whether the menu re-opens the card itself, once it is able to.
        /// <list type="bullet">
        /// <item>A party GUEST never does: only the host opens a card, and the host's open reaches the
        /// guest as the replicated lobby, so the whole party lands on the panel together. A guest's own
        /// open would be a request to play the card instead (<c>ArcadeGameConfigureModal.OpenFor</c>).</item>
        /// <item>Not while a guided path or the first-orientation funnel is running: those decide where
        /// the player goes next, and the quest resumes on HOME.</item>
        /// <item>Not once the player has gone somewhere themselves - into freestyle, or into a window.</item>
        /// </list>
        /// </summary>
        public static bool ShouldReopen(bool isPartyClient, bool guided, bool inFreestyle, bool windowOpen) =>
            !isPartyClient && !guided && !inFreestyle && !windowOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_armed = false;
            s_card = default;
        }
    }
}
