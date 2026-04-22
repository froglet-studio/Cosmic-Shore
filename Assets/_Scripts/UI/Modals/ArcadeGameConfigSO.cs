using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

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
            public int           TeamCount;
            public SO_Vessel     SelectedShip;
            public Domains       SelectedDomain;

            public void ResetState()
            {
                SelectedGame   = null;
                Intensity      = 0;
                PlayerCount    = 0;
                TeamCount      = 0;
                SelectedShip   = null;
                SelectedDomain = Domains.Unassigned;
            }
        }
}
