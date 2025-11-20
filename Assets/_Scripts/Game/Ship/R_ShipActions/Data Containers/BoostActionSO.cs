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
    
    public override void StartAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.IsBoosting = true;
        ShipStatus.IsStationary = false;
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.IsBoosting = false;
    }
}