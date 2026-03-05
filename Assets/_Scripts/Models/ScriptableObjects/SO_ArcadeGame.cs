using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.UserActions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Game", menuName = "CosmicShore/Game/ArcadeGame", order = 0)]
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

        [Header("Tips")]
        [Tooltip("Tips shown on the connecting panel before a match starts.")]
        public SO_GameModeTips Tips;
    }
}
