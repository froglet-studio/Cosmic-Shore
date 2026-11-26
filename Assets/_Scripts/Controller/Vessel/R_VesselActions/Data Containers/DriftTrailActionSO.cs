using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "DriftTrailAction", menuName = "ScriptableObjects/Vessel Actions/Drift Trail")]
    public class DriftTrailActionSO : ShipActionSO
    {
        /// <summary>It reshapes the trail prisms the hull lays, which every peer lays for itself: an AI's press must run on every peer (<see cref="ShipActionSO.AutopilotPressTravels"/>).</summary>
        public override bool AutopilotPressTravels => true;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<DriftTrailActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<DriftTrailActionExecutor>()?.End(this, vesselStatus);
    }
}
