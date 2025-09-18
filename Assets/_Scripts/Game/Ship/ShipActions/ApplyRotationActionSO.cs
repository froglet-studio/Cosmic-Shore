using UnityEngine;

[CreateAssetMenu(fileName = "ApplyRotationAction", menuName = "ScriptableObjects/Vessel Actions/ApplyRotationActionSO")]
public class ApplyRotationActionSO : ShipActionSO
{
    [SerializeField] float rotationAmount = 45f;
    [SerializeField] bool pitch = true;
    [SerializeField] bool yaw   = true;
    [SerializeField] bool roll  = true;

    public override void StartAction(ActionExecutorRegistry execs)
    {
        var ship = Ship;
        var status = ShipStatus;
        var transformer = status.VesselTransformer;

        if (pitch)
            transformer.ApplyRotation(rotationAmount, ship.Transform.right);

        if (yaw)
            transformer.ApplyRotation(rotationAmount, ship.Transform.up);

        if (roll)
            transformer.ApplyRotation(rotationAmount, ship.Transform.forward);
    }

    public override void StopAction(ActionExecutorRegistry execs)
    {
    }
}