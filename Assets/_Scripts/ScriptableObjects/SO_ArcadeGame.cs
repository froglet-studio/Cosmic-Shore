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

        [Min(1)] public int MinPlayers = 1;
        [Range(1, 3)] public int MaxPlayers = 2;
        [Min(1)] public int MinIntensity = 1;
        [Range(1, 4)] public int MaxIntensity = 4;
        public CallToActionTargetType CallToActionTargetType;
        public UserActionType ViewUserAction;
        public UserActionType PlayUserAction;
    }
}
