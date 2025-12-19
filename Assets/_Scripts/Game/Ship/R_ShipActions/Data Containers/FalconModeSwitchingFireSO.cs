using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName= "FalconModeSwitchingFire",
        menuName="ScriptableObjects/Vessel Actions/Falcon Mode Switching Fire")]
    public class FalconModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")]
        [SerializeField] private ShipActionSO speedMode;     // FullAutoActionSO
        [SerializeField] private ShipActionSO ringFire; // FullAutoBlockShootActionSO
        [SerializeField] private ShipActionSO creationMode; // PLACEHOLDER

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;
        private ActionExecutorRegistry _registry;
        private bool _isHeld;
        private int Selector = 1;
        private ShipActionSO _active;
        IVesselStatus _vesselStatus;


        public override void Initialize(IVessel ship)
        {
            base.Initialize(ship);
            _vesselStatus = ship.VesselStatus;
        }
        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
           
            _isHeld = true;
            _registry = execs;

            switch (Selector)
            {
                case 1:
                    _active = ringFire;
                    Selector = Selector + 1;
                    Debug.LogError($"Switch to: ringFire  {Selector}");
                    break;
                case 2:
                    _active = creationMode;
                    Selector = Selector + 1;
                    Debug.LogError($"Switch to: creationMode  {Selector}");
                    break;
                case 3:
                    _active = speedMode;
                    Selector = 1;
                    Debug.LogError($"Switch to: speedMode  {Selector}");
                    break;
            }

          
            //_active = vesselStatus is { IsTranslationRestricted: true } ? ringFire : speedMode;
            _active?.StartAction(execs, vesselStatus);
        }

        public override void StopAction(ActionExecutorRegistry registry, IVesselStatus vesselStatus)
        {
            _active?.StopAction(registry, _vesselStatus);
            _active = null;
        }
    }
}