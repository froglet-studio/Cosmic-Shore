using UnityEngine;
using CosmicShore.Core;

public class CustomZoomAction : ShipAction
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public ElementalFloat ZoomInRate;

    public override void StartAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
            CustomCameraController.Instance.ZoomCloseCameraOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        if (!Ship.ShipStatus.AutoPilotEnabled)
            CustomCameraController.Instance.ResetCloseCameraToNeutral(ZoomInRate.Value);
    }
}

