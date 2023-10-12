using UnityEngine;

class MantaAnimationContoller : ShipAnimation
{
    [SerializeField] Animator animator;

    float currentPitch = 0;
    float currentYaw = 0;
    float currentRoll = 0;
    float currentThrottle = 0;

    protected override void PerformShipAnimations(float pitch, float yaw, float roll, float throttle)
    {
        currentPitch = Mathf.Lerp(currentPitch, pitch * 10, Time.deltaTime);
        currentYaw = Mathf.Lerp(currentYaw, yaw * 10, Time.deltaTime);
        currentRoll = Mathf.Lerp(currentRoll, roll * 10, Time.deltaTime);
        currentThrottle = Mathf.Lerp(currentThrottle, throttle * 10, Time.deltaTime);

        animator.SetFloat("Pitch", currentPitch);
        animator.SetFloat("Yaw", currentYaw);
        animator.SetFloat("Roll", currentRoll);
        animator.SetFloat("Throttle", currentThrottle);
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