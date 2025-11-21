using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ModeSwitchingFire",
        menuName = "ScriptableObjects/Vessel Actions/Mode Switching Fire")]
    public class SparrowModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")] [SerializeField] private ShipActionSO normalFire;
        [SerializeField] private ShipActionSO stationaryFire;

        [Header("Events")] [SerializeField] private ScriptableEventBool stationaryModeChanged;

        private ShipActionSO _active;
        private ActionExecutorRegistry _registry;
        private bool _isHeld;
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

            _active = vesselStatus is { IsTranslationRestricted: true } ? stationaryFire : normalFire;
            _active?.StartAction(execs, vesselStatus);


            stationaryModeChanged.OnRaised += OnStationaryModeChanged;
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            stationaryModeChanged.OnRaised -= OnStationaryModeChanged;

            _isHeld = false;

            _active?.StopAction(execs,vesselStatus);
            _active = null;
            _registry = null;
        }

        private void OnStationaryModeChanged(bool isTranslationRestricted)
        {
            if (!_isHeld || !_registry)
                return;

            _active?.StopAction(_registry, _vesselStatus);
            _active = isTranslationRestricted ? stationaryFire : normalFire;
            _active?.StartAction(_registry, _vesselStatus);
        }
    }
}