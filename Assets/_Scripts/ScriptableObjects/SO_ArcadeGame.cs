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
                 "(Class Any = every hull; Intensity 0 = every intensity; a row naming a hull " +
                 "or an intensity wins over a wildcard one). LEAVE THIS EMPTY unless the card " +
                 "is solving its own balance: an empty table is published with the platform " +
                 "baseline instead - every hull at level 5 in all four elements on intensity 1, " +
                 "and at rest on 2-4. A card that authors ANY row owns its whole table and gets " +
                 "no baseline, so a partial table silently opts every other hull out of it. " +
                 "Published to every peer by the config sync and applied in " +
                 "VesselController.Initialize, so a guest's own vessel is seeded exactly as the " +
                 "host's replica of it. Authored by the card's generator from its balance " +
                 "model, never by hand.")]
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
