using UnityEngine;

public class ZoomOutAction : ShipActionAbstractBase
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public float ZoomInRate;

    public override void StartAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ZoomOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ResetToNeutral(ZoomInRate);
    }
}