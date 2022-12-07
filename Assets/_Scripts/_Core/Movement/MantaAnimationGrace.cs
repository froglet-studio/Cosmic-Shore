using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class MantaAnimationGrace : ShipAnimation
{

    [SerializeField]
    Transform Fusilage;

    [SerializeField]
    Transform LeftWing;

    [SerializeField]
    Transform RightWing;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;
    [SerializeField] float fusilageAnimationScaler = 25f;
    [SerializeField] float lerpAmount = 2f;

    public override void PerformShipAnimations(float yaw, float pitch, float throttle, float roll)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.

        AnimatePart(LeftWing,
                    (-throttle + roll - pitch) * animationScaler,
                    0,
                    -(throttle + yaw) * yawAnimationScaler);

        AnimatePart(RightWing,
                    -(throttle - roll + pitch) * animationScaler,
                    0,
                    (throttle - yaw) * yawAnimationScaler);

        AnimatePart(Fusilage,
                    -pitch * fusilageAnimationScaler,
                    roll * fusilageAnimationScaler,
                    -yaw * fusilageAnimationScaler);
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
