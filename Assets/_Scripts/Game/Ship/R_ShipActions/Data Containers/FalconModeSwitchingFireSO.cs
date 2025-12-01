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
        [SerializeField] private ShipActionSO ringMovement; // PLACEHOLDER
        private ShipActionSO _active;
        private IVesselStatus status;


        public override void Initialize(IVessel ship)
        {
            base.Initialize(ship);
            status = ship.VesselStatus;
        }
        public override void StartAction(ActionExecutorRegistry registry,IVesselStatus vesselStatus)
        {
            _active = vesselStatus is { IsTranslationRestricted: true } ? ringFire : normalFire;
            _active?.StartAction(registry, status);
        }

        public override void StopAction(ActionExecutorRegistry registry, IVesselStatus vesselStatus)
        {
            _active?.StopAction(registry, status);
            _active = null;
        }
    }
}