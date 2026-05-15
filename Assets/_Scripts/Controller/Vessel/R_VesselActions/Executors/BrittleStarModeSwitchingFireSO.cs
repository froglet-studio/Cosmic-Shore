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

        private ActionExecutorRegistry _registry;
        private bool _isHeld;
        private int _selector = 1;
        private ShipActionSO _active;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            base.Initialize(vs);
            _isHeld = true;
            _registry = execs;

            switch (_selector)
            {
                case 1: _active = ringFire; _selector = 2; break;
                case 2: _active = creationMode; _selector = 3; break;
                case 3: _active = speedMode; _selector = 1; break;
            }

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