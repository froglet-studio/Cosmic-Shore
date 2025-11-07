using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName="ModeSwitchingFire",
        menuName="ScriptableObjects/Vessel Actions/Mode Switching Fire")]
    public class SparrowModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")]
        [SerializeField] private ShipActionSO normalFire;   
        [SerializeField] private ShipActionSO stationaryFire;

        private ShipActionSO _active;

        public override void StartAction(ActionExecutorRegistry registry)
        {
            _active = ShipStatus is { IsTranslationRestricted: true } ? stationaryFire : normalFire;
            _active?.StartAction(registry);
        }

        public override void StopAction(ActionExecutorRegistry registry)
        {
            _active?.StopAction(registry);
            _active = null;
        }
    }
}