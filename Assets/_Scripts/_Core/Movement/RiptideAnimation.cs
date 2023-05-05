using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RiptideAnimation : ShipAnimation
{
    ShipData shipData;
    [SerializeField] Transform DriftHandle;
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

    public List<Transform> Transforms;

    

    static float animationScaler = 25f;
    float exaggeratedAnimationScaler = 3 * animationScaler;

    [SerializeField] float lerpAmount = 2f;
    [SerializeField] float smallLerpAmount = .7f;

    static Vector3 defaultThrusterPosition = new Vector3(0, .15f, -1.7f);
    Vector3 backwardThrusterPosition = defaultThrusterPosition - new Vector3(0, 0, 0);
    Vector3 defaultWingPosition = Vector3.zero;
    Vector3 forwardWingPosition = new Vector3(0, 0, 2.3f);

    private void Start()
    {
        shipData = GetComponent<ShipData>();
        Transforms.Add(DriftHandle);
        Transforms.Add(NoseTop);
        Transforms.Add(RightWing);
        Transforms.Add(NoseBottom);
        Transforms.Add(LeftWing);
        Transforms.Add(ThrusterTopRight);
        Transforms.Add(ThrusterRight);
        Transforms.Add(ThrusterBottomRight);
        Transforms.Add(ThrusterBottomLeft);
        Transforms.Add(ThrusterLeft);
        Transforms.Add(ThrusterTopLeft);
    }


    public override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        // Ship animations TODO: figure out how to leverage a single definition for pitch, etc. that captures the gyro in the animations.
        

        Vector3 wingPosition;
        Vector3 thrusterPosition;

        AnimatePart(Chassis,
                    pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler,
                    Vector3.zero);

        //AnimatePart(NoseTop,
        //            pitch * animationScaler,
        //            yaw * animationScaler,
        //            roll * animationScaler);

        //AnimatePart(NoseBottom,
        //            pitch * animationScaler,
        //            yaw * animationScaler,
        //            roll * animationScaler);
        if (shipData.Drifting)
        {
            DriftHandle.rotation = Quaternion.LookRotation(shipData.Course,transform.up);
            RightWing.parent = DriftHandle;
            LeftWing.parent = DriftHandle;
            wingPosition = forwardWingPosition;

            ThrusterTopRight.parent = DriftHandle;
            ThrusterRight.parent = DriftHandle;
            ThrusterBottomRight.parent = DriftHandle;
            ThrusterBottomLeft.parent = DriftHandle;
            ThrusterLeft.parent = DriftHandle;
            ThrusterTopLeft.parent = DriftHandle;
            thrusterPosition = backwardThrusterPosition;

        }
        else
        {
            RightWing.parent = Chassis;
            LeftWing.parent = Chassis; 
            wingPosition = defaultWingPosition;

            ThrusterTopRight.parent = Chassis;
            ThrusterRight.parent = Chassis;
            ThrusterBottomRight.parent = Chassis;
            ThrusterBottomLeft.parent = Chassis;
            ThrusterLeft.parent = Chassis;
            ThrusterTopLeft.parent = Chassis;
            thrusterPosition = defaultThrusterPosition;
        }

        AnimatePart(RightWing,
                    Brake(throttle) * animationScaler,
                    (yaw + throttle) * exaggeratedAnimationScaler,
                    (roll + pitch) * animationScaler,
                    wingPosition);

        
        AnimatePart(LeftWing,
                    Brake(throttle) * animationScaler,
                    (yaw - throttle) * exaggeratedAnimationScaler,
                    (roll - pitch) * animationScaler,
                    wingPosition);

        AnimatePart(ThrusterTopRight,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);


        AnimatePart(ThrusterRight,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);

        AnimatePart(ThrusterBottomRight,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);
        
        AnimatePart(ThrusterBottomLeft,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);
        
        AnimatePart(ThrusterLeft,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);

        AnimatePart(ThrusterTopLeft,
                    pitch * exaggeratedAnimationScaler,
                    yaw * exaggeratedAnimationScaler,
                    roll * exaggeratedAnimationScaler,
                    thrusterPosition);
    }

    public override void Idle()
    {
        foreach (Transform transform in Transforms)
        {
            resetAnimation(transform);
        }
    }

    void resetAnimation(Transform part) {part.localRotation = Quaternion.Lerp(part.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime); }


    void AnimatePart(Transform part, float partPitch, float partYaw, float partRoll, Vector3 partPosition)
    {
        part.localRotation = Quaternion.Lerp(
                                    part.localRotation,
                                    Quaternion.Euler(
                                        partPitch,
                                        partYaw,
                                        partRoll),
                                    lerpAmount * Time.deltaTime);
        part.localPosition = Vector3.Lerp(part.localPosition, partPosition, lerpAmount * Time.deltaTime);
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
