using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RiptideAnimation : ShipAnimation
{
    [SerializeField] Transform Chassis;

    [SerializeField] Transform NoseTop;
    [SerializeField] Transform RightWing;
    [SerializeField] Transform NoseBottom;
    [SerializeField] Transform LeftWing;
    
    [SerializeField] Transform ThrusterTopRight;
    [SerializeField] Transform ThrusterRight;
    [SerializeField] Transform ThrusterBottomRight;
    [SerializeField] Transform ThrusterBottomLeft;
    [SerializeField] Transform ThrusterLeft;
    [SerializeField] Transform ThrusterTopLeft;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 15f;
    [SerializeField] float rollAnimationScaler = 15f;
    [SerializeField] float lerpAmount = 2f;
    [SerializeField] float smallLerpAmount = .7f;


    public override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.

        AnimatePart(Chassis,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll - pitch) * rollAnimationScaler);


        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll + pitch) * rollAnimationScaler);

        AnimatePart(ThrusterLeft,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(ThrusterRight,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
    }

    public override void Idle()
    {
        LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        Chassis.localRotation = Quaternion.Lerp(Chassis.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        ThrusterLeft.localRotation = Quaternion.Lerp(ThrusterLeft.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        ThrusterRight.localRotation = Quaternion.Lerp(ThrusterRight.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
    }

    void AnimatePart(Transform part, float partPitch, float partYaw, float partRoll)
    {
        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    Quaternion.Euler(
                                        partPitch,
                                        partYaw,
                                        partRoll),
                                    lerpAmount * Time.deltaTime);
    }

    float Brake(float throttle)
    {
        var brakeThreshold = .65f;
        float newThrottle;
        if (throttle < brakeThreshold) newThrottle = throttle - brakeThreshold;
        else newThrottle = 0;
        return newThrottle;
    }
}
