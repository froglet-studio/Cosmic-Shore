using UnityEngine;

public class ZoomOutAction : ShipAction
{
    [SerializeField] private float zoomOutRate;
    [SerializeField] private ElementalFloat zoomInRate;

    public override void StartAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
            CameraManager.Instance.ZoomCloseCameraOut(zoomOutRate);
    }

    public override void StopAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
            CameraManager.Instance.ResetCloseCameraToNeutral(zoomInRate.Value);
    }
    public void SetZoomInRate(ElementalFloat newZoomInRate)
    {
        zoomInRate = newZoomInRate;
    }

}