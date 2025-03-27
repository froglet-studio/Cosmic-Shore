using CosmicShore.Game.AI;
using UnityEngine;

public class SingleStickShipTransformer : ShipTransformer
{
    Quaternion additionalRotation = Quaternion.identity;
    GameObject courseObject;
    Transform courseTransform;

    protected override void Start()
    {
        base.Start();
        Ship.ShipStatus.SingleStickControls = true;
        GetComponent<AIPilot>().SingleStickControls = true;


        courseObject = new GameObject("CourseObject");
        courseTransform = courseObject.transform;
    }

    protected override void Pitch() // These need to not use *= because quaternions are not commutative
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            -InputStatus.EasedLeftJoystickPosition.y * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
                            courseTransform.right) * accumulatedRotation;
    }

    protected override void Yaw()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + YawScaler) * Time.deltaTime,
                            courseTransform.up) * accumulatedRotation;
    }

    protected override void Roll()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            -InputStatus.EasedLeftJoystickPosition.x * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime, //use roll scaler to adjust the banking into turns
                            transform.forward) * accumulatedRotation;
    }

    protected override void RotateShip()
    {
        if (inputController != null)
        {

            Roll();
            Yaw();
            Pitch();
        }

        
        transform.rotation = Quaternion.Slerp(transform.rotation, accumulatedRotation, lerpAmount * Time.deltaTime);
        courseTransform = transform;
        shipStatus.Course = courseTransform.forward;
    }

    protected override void MoveShip()
    {
        float boostAmount = 1f;
        if (shipStatus.Boosting) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
        {
            boostAmount = Ship.ShipStatus.BoostMultiplier;
        }
        if (shipStatus.ChargedBoostDischarging) boostAmount *= shipStatus.ChargedBoostCharge;
        if (inputController != null)
            speed = Mathf.Lerp(speed, ThrottleScaler * boostAmount + MinimumSpeed, lerpAmount * Time.deltaTime);

        speed *= throttleMultiplier;
        shipStatus.Speed = speed;

        transform.position += (speed * shipStatus.Course + velocityShift) * Time.deltaTime;
    }
}