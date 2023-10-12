using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine;

class BufoAnimation : ShipAnimation
{
    [SerializeField] Transform Fusilage;
    //[SerializeField] Transform Turret;
    [SerializeField] Transform ThrusterTopRight;
    [SerializeField] Transform TopWing;
    [SerializeField] Transform ThrusterBottomRight;
    [SerializeField] Transform ThrusterBottomLeft;
    [SerializeField] Transform BottomWing;
    [SerializeField] Transform ThrusterTopLeft;    

    const float animationScalar = 32f;
    const float exaggeratedAnimationScalar = 1.4f * animationScalar;

    ShipStatus shipData;

    protected override void Start()
    {
        base.Start();

        shipData = GetComponent<ShipStatus>();
    }

    protected override void AssignTransforms()
    {
        Transforms.Add(Fusilage);
        //Transforms.Add(Turret);
        Transforms.Add(ThrusterTopRight);
        Transforms.Add(TopWing);
        Transforms.Add(ThrusterBottomRight);
        Transforms.Add(ThrusterBottomLeft);
        Transforms.Add(BottomWing);
        Transforms.Add(ThrusterTopLeft);
    }

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        var pitchScalar = pitch * exaggeratedAnimationScalar;
        var yawScalar = yaw * exaggeratedAnimationScalar;
        var rollScalar = roll * exaggeratedAnimationScalar;

        AnimatePart(Fusilage, pitch * animationScalar, yaw * animationScalar, roll * animationScalar);
        //AnimatePart(Turret, pitchScalar * .7f, yawScalar, rollScalar);

        foreach (var part in new List<Transform>() { ThrusterTopRight, TopWing, ThrusterBottomRight, ThrusterBottomLeft, BottomWing, ThrusterTopLeft })
            AnimatePart(part, pitchScalar, yawScalar, rollScalar);
    }

    protected override void AnimatePart(Transform part, float pitch, float yaw, float roll)
    {
        Quaternion rotation = shipData.Portrait ? Quaternion.Euler(yaw, -pitch, -roll) : Quaternion.Euler(pitch, yaw, roll);

        part.localRotation = Quaternion.Lerp(
                                part.localRotation,
                                rotation,
                                lerpAmount * Time.deltaTime);
    }
}