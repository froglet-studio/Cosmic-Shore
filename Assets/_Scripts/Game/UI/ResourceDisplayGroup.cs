using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class ResourceDisplayGroup : MonoBehaviour
    {
        [Header("HUD Containers")]
        public ResourceDisplay AmmoDisplay;
        public ResourceButton BoostDisplay;
        public ResourceDisplay EnergyDisplay;
        public ResourceDisplay ChargeLevelDisplay;
        public ResourceDisplay MassLevelDisplay;
        public ResourceDisplay SpaceLevelDisplay;
        public ResourceDisplay TimeLevelDisplay;
    }
}