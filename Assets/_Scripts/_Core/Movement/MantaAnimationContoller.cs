using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;
using UnityEngine.UIElements;

class MantaAnimationContoller : ShipAnimation
{
    [SerializeField] Animator animator;

    protected override void Start()
    {
        base.Start();
    }

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {

        animator.SetFloat("pitch", pitch*10);
        animator.SetFloat("yaw", yaw*10);
        animator.SetFloat("roll", roll*10);
        animator.SetFloat("throttle", throttle*10);
        animator.SetFloat("Blend", 1);
    }

    protected override void AssignTransforms()
    {
        throw new System.NotImplementedException();
    }
}