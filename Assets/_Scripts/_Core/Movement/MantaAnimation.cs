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

    public override void PerformShipAnimations(float Xsum, float Ysum, float Xdiff, float Ydiff)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
        LeftWing.localRotation = Quaternion.Lerp(
                                    LeftWing.localRotation,
                                    Quaternion.Euler(
                                        (Ydiff - Ysum) * animationScaler, //tilt based on pitch and roll
                                        0,
                                        -(Xdiff + Xsum) * yawAnimationScaler), //sweep back based on throttle and yaw
                                    lerpAmount * Time.deltaTime);

        RightWing.localRotation = Quaternion.Lerp(
                                    RightWing.localRotation,
                                    Quaternion.Euler(
                                        -(Ydiff + Ysum) * animationScaler,
                                        0,
                                        (Xdiff - Xsum) * yawAnimationScaler),
                                    lerpAmount * Time.deltaTime);

        Fusilage.localRotation = Quaternion.Lerp(
                                    Fusilage.localRotation,
                                    Quaternion.Euler(
                                        -Ysum * animationScaler,
                                        Ydiff * animationScaler,
                                        -Xsum * animationScaler),
                                    lerpAmount * Time.deltaTime);
    }
}
