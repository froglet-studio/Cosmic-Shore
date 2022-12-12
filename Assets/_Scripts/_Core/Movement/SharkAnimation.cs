using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class SharkAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;

    [SerializeField] Transform LeftWing;

    [SerializeField] Transform RightWing;

    [SerializeField] Transform Head;

    [SerializeField] Transform Tail;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;
    [SerializeField] float lerpAmount = 2f;
    [SerializeField] float smallLerpAmount = .7f;

    public override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.

        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(LeftWing,
                    Brake(throttle) * yawAnimationScaler,
                    0,
                    (roll + pitch) * animationScaler);
                    

        AnimatePart(RightWing,
                    Brake(throttle) * yawAnimationScaler,
                    0,
                    (roll - pitch) * animationScaler);

        AnimatePart(Head,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(Tail,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);
    }

    public override void Idle()
    {
        LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
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
