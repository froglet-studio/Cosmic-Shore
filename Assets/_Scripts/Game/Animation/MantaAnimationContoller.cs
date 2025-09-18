using UnityEngine;

namespace CosmicShore.Game.Animation
{
    class MantaAnimationContoller : ShipAnimation
    {
        [SerializeField] Animator animator;
        [SerializeField] bool hasBoost = false;

        float currentPitch = 0;
        float currentYaw = 0;
        float currentRoll = 0;
        float currentThrottle = 0;
        float animationSpeed = 3.25f;

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            if (VesselStatus.Boosting && hasBoost) animator.SetBool("Boost", true);
            else if (hasBoost) animator.SetBool("Boost", false);

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
            if (VesselStatus.Boosting) animator.SetBool("Boost", true);
            else animator.SetBool("Boost", false);

            currentPitch = Mathf.Lerp(currentPitch, 0, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, 0, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, 0, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, 0, animationSpeed * Time.deltaTime);

            animator.SetFloat("Pitch", currentPitch);
            animator.SetFloat("Yaw", currentYaw);
            animator.SetFloat("Roll", currentRoll);
            animator.SetFloat("Throttle", currentThrottle);
        }

        protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
    }
}