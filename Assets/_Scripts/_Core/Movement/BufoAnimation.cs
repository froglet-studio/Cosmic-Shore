using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class BufoAnimation : ShipAnimation
{

    [SerializeField] Transform Fusilage;
    [SerializeField] Transform Turret;

    [SerializeField] Transform ThrusterTopRight;
    [SerializeField] Transform ThrusterRight;
    [SerializeField] Transform ThrusterBottomRight;
    [SerializeField] Transform ThrusterBottomLeft;
    [SerializeField] Transform ThrusterLeft;
    [SerializeField] Transform ThrusterTopLeft;

    public List<Transform> Transforms; //TODO: use this to populate the ship geometries on ship.cs 

    static float animationScaler = 32f;
    float exaggeratedAnimationScaler = 1.4f * animationScaler;
    [SerializeField] float lerpAmount = 2f;
    [SerializeField] float smallLerpAmount = .7f;

    ShipData shipData;

    private void Start()
    {
        shipData = GetComponent<ShipData>();

        Transforms.Add(Fusilage);
        Transforms.Add(Turret);
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

        AnimatePart(Fusilage,
            pitch * animationScaler,
            yaw * animationScaler,
            roll * animationScaler);

        AnimatePart(Turret,
            pitch * exaggeratedAnimationScaler*.7f,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterTopRight,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterRight,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterBottomRight,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterBottomLeft,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterLeft,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);

        AnimatePart(ThrusterTopLeft,
            pitch * exaggeratedAnimationScaler,
            yaw * exaggeratedAnimationScaler,
            roll * exaggeratedAnimationScaler);
    }

    public override void Idle()
    {
        foreach (Transform transform in Transforms)
        {
            resetAnimation(transform);
        }
    }

    void resetAnimation(Transform part) { part.localRotation = Quaternion.Lerp(part.localRotation, Quaternion.identity, smallLerpAmount * Time.deltaTime); }

    void AnimatePart(Transform part, float partPitch, float partYaw, float partRoll)
    {
        Quaternion rotation;
        if (shipData.Portrait)
        {
            rotation = Quaternion.Euler(
                                            partYaw,
                                            -partPitch,
                                            -partRoll);
        }
        else
        {
            rotation = Quaternion.Euler(
                                        partPitch,
                                        partYaw,
                                        partRoll);
        }

        part.localRotation = Quaternion.Lerp(
                                part.localRotation,
                                rotation,
                                lerpAmount * Time.deltaTime);
        
    }

}
