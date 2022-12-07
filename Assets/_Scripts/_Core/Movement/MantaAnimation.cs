using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class MantaAnimation : ShipAnimation
{

    [SerializeField]
    Transform Fusilage;

    [SerializeField]
    Transform LeftWing;

    [SerializeField]
    Transform RightWing;

    private readonly float animationScaler = 25f;
    private readonly float yawAnimationScaler = 80f;
    private readonly float lerpAmount = 2f;

    public override void PerformShipAnimations(float yaw, float pitch, float throttle, float roll)
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
