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
        scaledParts.Add(ShroudBottomLeft);
        scaledParts.Add(ShroudBottomRight);
        scaledParts.Add(ShroudLeft);
        scaledParts.Add(ShroudRight);
        scaledParts.Add(LeftGun);
        scaledParts.Add(RightGun);
        scaledParts.Add(JetTopLeft);
        scaledParts.Add(JetTopRight);
        scaledParts.Add(ShroudTopLeft);
        scaledParts.Add(ShroudTopRight);
    }

    protected override void Update()
    {
        base.Update();

        if (GetComponent<Ship>().ShipData.Attached)
        {
            ResetParts();        }
        else
        {
            ScaleParts(1.75f);
        }
    }

    void ScaleParts(float scale) 
    {
        foreach (Transform transform in scaledParts)
        {
            transform.localScale = Vector3.Lerp(transform.localScale,new Vector3(scale, scale, scale),.1f);
        }
    }

    void ResetParts()
    {
        foreach (Transform transform in scaledParts)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, .1f);
        }
    }

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        //AnimatePart(LeftWing,
        //            Brake(throttle) * yawAnimationScaler,
        //            -(throttle - yaw) * yawAnimationScaler,
        //            (roll - pitch) * animationScaler);

        //AnimatePart(RightWing,
        //            Brake(throttle) * yawAnimationScaler,
        //            (throttle + yaw) * yawAnimationScaler,
        //            (roll + pitch) * animationScaler);

        //AnimatePart(Fusilage,
        //            pitch * animationScaler,
        //            yaw * animationScaler,
        //            roll * animationScaler);
    }

    protected override void AssignTransforms()
    {
        //Transforms.Add(Fusilage);
        //Transforms.Add(LeftWing);
        //Transforms.Add(RightWing);
    }
}