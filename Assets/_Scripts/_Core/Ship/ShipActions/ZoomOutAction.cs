using UnityEngine;

public class ZoomOutAction : LevelAwareShipAction
{
    [SerializeField] public float ZoomOutRate;
    [SerializeField] public float ZoomInRate;

    public override void StartAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ZoomCloseCameraOut(ZoomOutRate);
    }

    public override void StopAction()
    {
        if (!ship.ShipStatus.AutoPilotEnabled) ship.cameraManager.ResetCloseCameraToNeutral(ZoomInRate);
    }

    public override void SetLevelParameter(Element element, float amount)
    {
        switch (element)
        {
            case Element.Charge:
                ZoomOutRate = amount;
                break;
            case Element.Mass:
                break;
            case Element.Space:
                break;
            case Element.Time:
                break;
        }
    }
}