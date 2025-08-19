using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>Lives on the ship prefab. Drag local action components here.</summary>
    public class ShipHUDRefs : MonoBehaviour
    {
        [Header("Dolphin")]
        public ChargeBoostAction chargeBoost;

        [Header("Serpent")]
        public ConsumeBoostAction consumeBoost;
        public SeedAssemblerAction seedAssembler;

        [Header("Sparrow")]
        public OverheatingAction overheating;
        public FullAutoAction fullAuto;
        public FireGunAction fireGun;
        public ToggleStationaryModeAction stationary;

        // add more when you need them
    }
}