using UnityEngine;

public class ZoomOutAction : ShipAction
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public ElementalFloat ZoomInRate;

    public override void StartAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ZoomCloseCameraOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ResetCloseCameraToNeutral(ZoomInRate.Value);
    }

}