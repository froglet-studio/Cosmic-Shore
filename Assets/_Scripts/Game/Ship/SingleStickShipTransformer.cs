using CosmicShore.Game;
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
        
        courseObject = new GameObject("CourseObject");
        courseTransform = courseObject.transform;
    }

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        Ship.ShipStatus.SingleStickControls = true;
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
        Roll();
        Yaw();
        Pitch();

        transform.rotation = Quaternion.Slerp(transform.rotation, accumulatedRotation, LERP_AMOUNT * Time.deltaTime);
        courseTransform = transform;
        shipStatus.Course = courseTransform.forward;
    }

    protected override void MoveShip()
    {
        float boostAmount = 1f;
        if (shipStatus.Boosting) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
            boostAmount = Ship.ShipStatus.BoostMultiplier;
        
        if (shipStatus.ChargedBoostDischarging) 
            boostAmount *= shipStatus.ChargedBoostCharge;
        
        speed = Mathf.Lerp(speed, ThrottleScaler * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);

        speed *= throttleMultiplier;

        if (toggleManualThrottle)
            speed = Mathf.Lerp(0, speed, InputStatus.Throttle);

        shipStatus.Speed = speed;

        transform.position += (speed * shipStatus.Course + velocityShift) * Time.deltaTime;
    }
}