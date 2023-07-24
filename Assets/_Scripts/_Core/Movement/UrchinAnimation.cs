using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine.UIElements;

class UrchinAnimation : ShipAnimation
{

    [SerializeField] Transform Body;
    [SerializeField] Transform JetBottomLeft;
    [SerializeField] Transform JetBottomRight;
    [SerializeField] Transform ShroudBottomLeft;
    [SerializeField] Transform ShroudBottomRight;
    [SerializeField] Transform ShroudLeft;
    [SerializeField] Transform ShroudRight;
    [SerializeField] Transform LeftGun;
    [SerializeField] Transform RightGun;
    [SerializeField] Transform JetTopLeft;
    [SerializeField] Transform JetTopRight;
    [SerializeField] Transform ShroudTopLeft;
    [SerializeField] Transform ShroudTopRight;

    [SerializeField] float animationScaler = 25f;
    [SerializeField] float yawAnimationScaler = 80f;

    protected List<Transform> scaledParts = new();

    protected override void Start()
    {
        base.Start();
        scaledParts.Add(JetBottomLeft);
        scaledParts.Add(JetBottomRight);
        scaledParts.Add(JetTopLeft);
        scaledParts.Add(JetTopRight);
    }

    protected override void Update()
    {
        base.Update();

        if (GetComponent<Ship>().ShipData.Attached)
        {
            ResetParts(scaledParts, 6);
        }
        else
        {
            ScaleParts(scaledParts, 1.75f, 6);
        }
    }

    void ScaleParts(List<Transform> transforms, float scale, float speed) 
    {
        foreach (Transform transform in transforms)
        {
            transform.localScale = Vector3.Lerp(transform.localScale,new Vector3(scale, scale, scale), speed * Time.deltaTime);
        }
    }

    void ResetParts(List<Transform> transforms, float speed)
    {
        foreach (Transform transform in transforms)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, speed * Time.deltaTime);
        }
    }

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        //AnimatePart(LeftGun,
        //            Brake(throttle) * yawAnimationScaler,
        //            -(throttle - yaw) * yawAnimationScaler,
        //            (roll + pitch) * animationScaler);

        //AnimatePart(RightGun,
        //            Brake(throttle) * yawAnimationScaler,
        //            (throttle + yaw) * yawAnimationScaler,
        //            (roll - pitch) * animationScaler);

        AnimatePart(Body,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(LeftGun,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(RightGun,
                    -pitch * animationScaler,
                    yaw * animationScaler,
                    roll * animationScaler);

        AnimatePart(JetBottomLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll + pitch + (3 * (1 - throttle))) * animationScaler);

        AnimatePart(JetTopRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll + pitch + (3 * (1 - throttle))) * animationScaler);

        AnimatePart(JetTopLeft,
                    Brake(throttle) * yawAnimationScaler,
                    -(throttle - yaw) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler);

        AnimatePart(JetBottomRight,
                    Brake(throttle) * yawAnimationScaler,
                    (throttle + yaw) * yawAnimationScaler,
                    (roll - pitch - (3 * (1 - throttle))) * animationScaler);
    }

    protected override void AssignTransforms()
    {
        //Transforms.Add(Fusilage);
        //Transforms.Add(LeftWing);
        //Transforms.Add(RightWing);
    }
}