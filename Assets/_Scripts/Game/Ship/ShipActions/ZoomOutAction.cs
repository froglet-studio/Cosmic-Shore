using UnityEngine;

public class ZoomOutAction : ShipAction
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public ElementalFloat ZoomInRate;

    public override void StartAction()
    {
        // if (!Ship.ShipStatus.AutoPilotEnabled) /*Ship.ShipStatus.*/ CameraManager.Instance.ZoomCloseCameraOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        // if (!Ship.ShipStatus.AutoPilotEnabled) /*Ship.ShipStatus.*/ CameraManager.Instance.ResetCloseCameraToNeutral(ZoomInRate.Value);
    }

}