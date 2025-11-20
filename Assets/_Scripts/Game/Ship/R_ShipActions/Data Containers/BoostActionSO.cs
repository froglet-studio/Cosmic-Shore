using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "BoostAction", menuName = "ScriptableObjects/Vessel Actions/Boost")]
public class BoostActionSO : ShipActionSO
{
    public override void Initialize(IVessel ship)
    {
        base.Initialize(ship);
    }
    
    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        vesselStatus.Boosting = true;
        vesselStatus.IsStationary = false;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        if (vesselStatus == null) return;
        vesselStatus.Boosting = false;
    }
}