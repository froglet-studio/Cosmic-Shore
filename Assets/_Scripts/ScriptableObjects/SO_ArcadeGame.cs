using CosmicShore.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Game", menuName = "ScriptableObjects/Game/ArcadeGame", order = 0)]
    [System.Serializable]
    public class SO_ArcadeGame : SO_Game
    {
        [FormerlySerializedAs("Captains")]
        public List<SO_Vessel> Vessels;

        public int MinPlayersAllowed = 1;
        public int MaxPlayersAllowed = 2;
        [Tooltip("Minimum number of domains (teams) this mode requires. Modes that need opposing teams (e.g. Joust) set this to 2 so the lobby can never launch with every player on one domain - which would leave no opponents.")]
        [Range(1, 3)] public int MinDomainsAllowed = 1;
        [Tooltip("Maximum number of domains (teams) this mode allows. Modes with a fixed team shape (e.g. Astro League needs exactly two domains for its two goals) set this equal to MinDomainsAllowed to pin the count.")]
        [Range(1, 3)] public int MaxDomainsAllowed = 3;
        [Min(1)] public int MinIntensity = 1;
        [Range(1, 4)] public int MaxIntensity = 4;

        [Header("Arena rules")]
        [Tooltip("ARENA seating (Docs/HomeHub/ARCHITECTURE.md §3.7). On: every hull in this " +
                 "match is flown by exactly ONE pilot - once a pilot or an AI has a vessel class " +
                 "nobody else may take it - and a human may hand their ship to the AI and take " +
                 "over an AI teammate's hull mid-match (D-pad left/right, keyboard 1/2). Seats " +
                 "are therefore capped at the number of hulls this card lists (MaxSeats). Set " +
                 "on the cards in the ArenaGames roster; an arcade card that pins one hull " +
                 "leaves it off.")]
        public bool ArenaRules;

        /// <summary>
        /// The most pilots (human + AI) this card can seat. <see cref="MaxPlayersAllowed"/> for
        /// every card except an <see cref="ArenaRules"/> card, where every hull is flown by one
        /// pilot and a card listing N distinct hulls therefore seats at most N - a seventh pilot
        /// on a six-hull card would have no hull left to fly.
        /// </summary>
        public int MaxSeats
        {
            get
            {
                if (!ArenaRules) return MaxPlayersAllowed;
                int hulls = DistinctHullCount;
                return hulls > 0 ? Mathf.Min(MaxPlayersAllowed, hulls) : MaxPlayersAllowed;
            }
        }

        /// <summary>How many distinct vessel classes <see cref="Vessels"/> names.</summary>
        public int DistinctHullCount
        {
            get
            {
                if (Vessels == null) return 0;
                var seen = new HashSet<VesselClassType>();
                for (int i = 0; i < Vessels.Count; i++)
                    if (Vessels[i] != null) seen.Add(Vessels[i].Class);
                return seen.Count;
            }
        }

        [Header("Briefing")]
        [Tooltip("Short play tips shown one at a time under the description on the launch panel. " +
                 "Empty is fine - the panel then shows the description alone rather than an " +
                 "empty 'Tip:' line. Write them as advice a player can act on in the first " +
                 "thirty seconds, not as lore.")]
        [TextArea(1, 3)] public List<string> Tips = new();

        [Header("Maelstrom video")]
        [Tooltip("Clip shown in the Maelstrom launch panel's frame, where the live preview " +
                 "window cannot go: the meta-mode draws OTHER modes, so it has no arena of its " +
                 "own to stand up. EVERY OTHER CARD LEAVES THIS EMPTY - a playable mode previews " +
                 "live (Docs/ModePreview/ARCHITECTURE.md) and must never fall back to a video.")]
        public VideoClip PreviewVideo;

        public UserActionType ViewUserAction;
        public UserActionType PlayUserAction;

        [Header("Starting elements (arena cards)")]
        [Tooltip("Element levels each HULL starts this card's match at - the platform's " +
                 "handicap dial for a card that seats several vessels. One row per hull " +
                 "(Intensity 0 = every intensity; 1-4 = that intensity only, winning over the 0 " +
                 "row). A hull with no row starts at rest (every element level 0), which is " +
                 "also what every single-hull card gets by leaving this empty. Published to " +
                 "every peer by the config sync and applied in VesselController.Initialize, so " +
                 "a guest's own vessel is seeded exactly as the host's replica of it. Authored " +
                 "by the card's generator from its balance model, never by hand.")]
        public List<VesselStartingElements> StartingElements = new();

        [Header("Elemental Comeback (required for every party game)")]
        [Tooltip("Levels of ALL FOUR elements a trailing player/team gains per unit of score " +
                 "deficit behind first place (leaderScore - yourTeamScore, in this mode's " +
                 "scoring stat). Applied equally to Charge/Mass/Space/Time by the required " +
                 "ElementalComebackSystem; the comeback layer can never lift an element above " +
                 "level 10. 0 disables comeback for this game.")]
        [Min(0f)] public float ComebackRatePerScoreDeficit = 1f;
    }
}
