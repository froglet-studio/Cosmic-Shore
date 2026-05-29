using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "BrittleStarModeSwitchingFire",
        menuName = "ScriptableObjects/Vessel Actions/BrittleStar Mode Switching Fire")]
    public class BrittleStarModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")]
        [SerializeField] private ShipActionSO speedMode;
        [SerializeField] private ShipActionSO ringFire;
        [SerializeField] private ShipActionSO creationMode;

        [Header("Ring Fire Rate")]
        [Tooltip("Minimum seconds between ring fire bursts when mode-switching triggers it.")]
        [SerializeField] private float ringFireInterval = 1f;

        private ActionExecutorRegistry _registry;
        private bool _isHeld;
        private int _selector = 1;
        private ShipActionSO _active;
        private float _lastRingFireTime = float.MinValue;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            base.Initialize(vs);
            _isHeld = true;
            _registry = execs;

            ShipActionSO next;
            int nextSelector;
            switch (_selector)
            {
                case 1: next = ringFire;      nextSelector = 2; break;
                case 2: next = creationMode;  nextSelector = 3; break;
                case 3: next = speedMode;     nextSelector = 1; break;
                default: return;
            }

            if (next == ringFire && Time.time - _lastRingFireTime < ringFireInterval)
                return;

            _active = next;
            _selector = nextSelector;

            if (_active == ringFire)
                _lastRingFireTime = Time.time;

            _active?.StartAction(execs, vesselStatus);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            _isHeld = false;
            _active?.StopAction(execs, vesselStatus);
            _active = null;
            _registry = null;
        }
    }
}