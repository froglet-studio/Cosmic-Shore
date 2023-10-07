using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;
using System.Collections;
using Unity.Mathematics;



public class SingleStickShipTransformer : ShipTransformer
{
    Quaternion additionalRotation = Quaternion.identity;
    GameObject courseObject;
    Transform courseTransform;
    [SerializeField] float lookScalar = 90;

    protected override void Start()
    {
        base.Start();
        ship.ShipStatus.SingleStickControls = true;
     
        courseObject = new GameObject("CourseObject");
        courseTransform = courseObject.transform;
    }

    protected override void Pitch() // These need to not use *= because quaternions are not commutative
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            -inputController.LeftJoystickPosition.y * (speed * RotationThrottleScaler + PitchScaler) * Time.deltaTime,
                            courseTransform.right) * accumulatedRotation;
        additionalRotation = Quaternion.AngleAxis(
                            -inputController.RightJoystickPosition.y * lookScalar,
                            courseTransform.right) * additionalRotation;
    }

    protected override void Yaw()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            inputController.LeftJoystickPosition.x * (speed * RotationThrottleScaler + YawScaler) * Time.deltaTime,
                            courseTransform.up) * accumulatedRotation;
        additionalRotation = Quaternion.AngleAxis(
                            inputController.RightJoystickPosition.x * lookScalar,
                            courseTransform.up) * additionalRotation;
    }

    protected override void Roll()
    {
        accumulatedRotation = Quaternion.AngleAxis(
                            -inputController.LeftJoystickPosition.x * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime, //use roll scaler to adjust the banking into turns
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

        courseTransform.rotation = Quaternion.Lerp(courseTransform.rotation, accumulatedRotation, lerpAmount);
        transform.rotation = Quaternion.Lerp(transform.rotation, additionalRotation * accumulatedRotation, Time.deltaTime);

        additionalRotation = Quaternion.identity;

        shipData.Course = courseTransform.forward;
        Debug.Log($"shipData.Course {shipData.Course} Transform.forward {transform.forward}");
    }

    protected override void MoveShip()
    {
        float boostAmount = 1f;
        if (shipData.Boosting && resourceSystem.CurrentBoost > 0) // TODO: if we run out of fuel while full speed and straight the ship data still thinks we are boosting
        {
            boostAmount = ship.boostMultiplier;
            resourceSystem.ChangeBoostAmount(ship.boostFuelAmount);
        }
        if (shipData.ChargedBoostDischarging) boostAmount *= shipData.ChargedBoostCharge;
        if (inputController != null)
            speed = Mathf.Lerp(speed, inputController.XDiff * ThrottleScaler * boostAmount + MinimumSpeed, lerpAmount * Time.deltaTime);

        speed *= throttleMultiplier;
        shipData.Speed = speed;

        transform.position += (speed * shipData.Course + velocityShift) * Time.deltaTime;
    }



}