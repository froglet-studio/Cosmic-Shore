using CosmicShore.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        public CallToActionTargetType CallToActionTargetType;
        public UserActionType ViewUserAction;
        public UserActionType PlayUserAction;

        [Header("Elemental Comeback (required for every party game)")]
        [Tooltip("Levels of ALL FOUR elements a trailing player/team gains per unit of score " +
                 "deficit behind first place (leaderScore - yourTeamScore, in this mode's " +
                 "scoring stat). Applied equally to Charge/Mass/Space/Time by the required " +
                 "ElementalComebackSystem; the comeback layer can never lift an element above " +
                 "level 10. 0 disables comeback for this game.")]
        [Min(0f)] public float ComebackRatePerScoreDeficit = 1f;
    }
}
