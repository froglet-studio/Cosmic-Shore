using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName= "FalconModeSwitchingFire",
        menuName="ScriptableObjects/Vessel Actions/Falcon Mode Switching Fire")]
    public class FalconModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")]
        [SerializeField] private ShipActionSO normalFire;     // FullAutoActionSO
        [SerializeField] private ShipActionSO ringFire; // FullAutoBlockShootActionSO
        [SerializeField] private ShipActionExecutorBase ringMovement; // PLACEHOLDER
        private ShipActionSO _active;

        public override void StartAction(ActionExecutorRegistry registry)
        {
            _active = ShipStatus is { IsTranslationRestricted: true } ? ringFire : normalFire ;
            _active?.StartAction(registry);
        }

        public override void StopAction(ActionExecutorRegistry registry)
        {
            _active?.StopAction(registry);
            _active = null;
        }
    }
}