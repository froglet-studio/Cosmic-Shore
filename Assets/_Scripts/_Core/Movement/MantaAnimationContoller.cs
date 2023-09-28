using UnityEngine;

class MantaAnimationContoller : ShipAnimation
{
    [SerializeField] Animator animator;

    protected override void Start()
    {
        base.Start();
    }

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        animator.SetFloat("Pitch", pitch*10);
        animator.SetFloat("Yaw", yaw*10);
        animator.SetFloat("Roll", roll*10);
        animator.SetFloat("Throttle", throttle*10);
        animator.SetFloat("Blend", 1);
    }

    protected override void Idle()
    {
        animator.SetFloat("Pitch", 0);
        animator.SetFloat("Yaw", 0);
        animator.SetFloat("Roll", 0);
        animator.SetFloat("Throttle", 0);
        animator.SetFloat("Blend", 1);
    }

    protected override void AssignTransforms(){ /* NOOP Abstract Implementation */ }
}