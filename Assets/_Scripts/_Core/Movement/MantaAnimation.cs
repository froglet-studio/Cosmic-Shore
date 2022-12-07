using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class MantaAnimation : ShipAnimation
{

    [SerializeField] Transform Fusilage;

    [SerializeField] Transform LeftWing;

    [SerializeField] Transform RightWing;

    readonly float animationScaler = 25f;
    readonly float yawAnimationScaler = 80f;
    readonly float lerpAmount = 2f;
    readonly float smallLerpAmount = .7f;

    public override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.

        AnimatePart(LeftWing,
                    (roll - pitch) * animationScaler,
                    0,
                    -(throttle + yaw) * yawAnimationScaler);

        AnimatePart(RightWing,
                    -(roll + pitch) * animationScaler,
                    0,
                    (throttle - yaw) * yawAnimationScaler);

        AnimatePart(Fusilage,
                    -pitch * animationScaler,
                    roll * animationScaler,
                    -yaw * animationScaler);
    }

    public override void Idle()
    {
        LeftWing.localRotation = Quaternion.Lerp(LeftWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        RightWing.localRotation = Quaternion.Lerp(RightWing.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
        Fusilage.localRotation = Quaternion.Lerp(Fusilage.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime);
    }

    void AnimatePart(Transform part, float partPitch, float partRoll, float partYaw)
    {
        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    Quaternion.Euler(
                                        partPitch,
                                        partRoll, 
                                        partYaw),  
                                    lerpAmount * Time.deltaTime);
    }
}
