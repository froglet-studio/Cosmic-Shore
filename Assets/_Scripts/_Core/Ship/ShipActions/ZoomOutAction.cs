using UnityEngine;

public class ZoomOutAction : ShipActionAbstractBase
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public float ZoomInRate;

    public override void StartAction()
    {
        ship.cameraManager.ZoomOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        ship.cameraManager.ResetToNeutral(ZoomInRate);
    }
}