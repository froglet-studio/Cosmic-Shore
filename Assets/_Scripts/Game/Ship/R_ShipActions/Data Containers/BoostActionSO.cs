using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName = "BoostAction", menuName = "CosmicShore/Actions/Boost")]
public class BoostActionSO : ShipActionSO
{
    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
    }
    
    public override void StartAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.Boosting = true;
        ShipStatus.IsStationary = false;
        Debug.Log($"[BoostActionSO] Boost started on ship: {ShipStatus.PlayerName}");
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
        if (ShipStatus == null) return;
        ShipStatus.Boosting = false;
        Debug.Log($"[BoostActionSO] Boost stopped on ship: {ShipStatus.PlayerName}");
    }
}