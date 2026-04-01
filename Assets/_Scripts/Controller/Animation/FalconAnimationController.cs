using UnityEngine;

namespace CosmicShore.Gameplay
{
    class FalconAnimationController : VesselAnimation
    {
        [SerializeField] Animator animator;

        float currentPitch;
        float currentYaw;
        float currentRoll;
        float currentThrottle;
        float animationSpeed = 3.25f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            currentPitch = Mathf.Lerp(currentPitch, pitch, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, yaw, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, roll, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, throttle, animationSpeed * Time.deltaTime);

            animator.SetFloat("Pitch", -currentPitch);
            animator.SetFloat("Yaw", currentYaw);
            animator.SetFloat("Roll", currentRoll);
            animator.SetFloat("Throttle", currentThrottle);
        }

        protected override void Idle()
        {
            if (VesselStatus.IsBoosting) animator.SetBool("Boost", true);
            else animator.SetBool("Boosting", false);

            currentPitch = Mathf.Lerp(currentPitch, 0, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, 0, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, 0, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, 0, animationSpeed * Time.deltaTime);

            animator.SetFloat("Pitch", -currentPitch);
            animator.SetFloat("Yaw", currentYaw);
            animator.SetFloat("Roll", currentRoll);
            animator.SetFloat("Throttle", currentThrottle);
        }

        protected override void AssignTransforms() { }
    }
}
