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
        [Tooltip("Minimum number of domains (teams) this mode requires. Modes that need opposing teams (e.g. Joust) set this to 2 so the lobby can never launch with every player on one domain — which would leave no opponents.")]
        [Range(1, 3)] public int MinDomainsAllowed = 1;
        [Min(1)] public int MinIntensity = 1;
        [Range(1, 4)] public int MaxIntensity = 4;
        public CallToActionTargetType CallToActionTargetType;
        public UserActionType ViewUserAction;
        public UserActionType PlayUserAction;
    }
}
