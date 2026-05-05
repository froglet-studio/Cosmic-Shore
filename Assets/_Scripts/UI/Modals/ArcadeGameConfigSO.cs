using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.UI
{
    [CreateAssetMenu(
            fileName = "ArcadeGameConfig",
            menuName = "ScriptableObjects/Arcade/ArcadeGameConfig")]
        public class ArcadeGameConfigSO : ScriptableObject
        {
            [Header("Runtime State")]
            public SO_ArcadeGame SelectedGame;
            public int           Intensity;
            public int           PlayerCount;
            [FormerlySerializedAs("TeamCount")]
            public int           DomainCount;
            public SO_Vessel     SelectedShip;
            public Domains       SelectedDomain;

            public void ResetState()
            {
                SelectedGame   = null;
                Intensity      = 0;
                PlayerCount    = 0;
                DomainCount    = 0;
                SelectedShip   = null;
                SelectedDomain = Domains.Blue;
            }
        }
}
