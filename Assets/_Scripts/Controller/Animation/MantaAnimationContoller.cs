using UnityEngine;

namespace CosmicShore.Gameplay
{
    class MantaAnimationContoller : VesselAnimation
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
            if (VesselStatus.IsBoosting && hasBoost) animator.SetBool("Boost", true);
            else if (hasBoost) animator.SetBool("Boost", false);

            currentPitch = Mathf.Lerp(currentPitch, pitch, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, yaw, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, roll, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, throttle, animationSpeed * Time.deltaTime);

            WriteAnimatorPose();
        }

        protected override void Idle()
        {
            if (VesselStatus.IsBoosting) animator.SetBool("Boost", true);
            else if (hasBoost) animator.SetBool("Boost", false);

            currentPitch = Mathf.Lerp(currentPitch, 0, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, 0, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, 0, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, 0, animationSpeed * Time.deltaTime);

            WriteAnimatorPose();
        }

        /// <summary>The ONE writer of the animator's stick floats, for puppetry and Idle alike. Idle used to write the
        /// eased terms unscaled and with pitch unflipped, so on a phone (where lifting the thumb is an instant Idle)
        /// the pose jumped on the first idle frame: pitch flipped sign and the rest halved (vessel skill rule 53).</summary>
        void WriteAnimatorPose()
        {
            animator.SetFloat("Pitch", -currentPitch * 2);
            animator.SetFloat("Yaw", currentYaw * 2);
            animator.SetFloat("Roll", currentRoll * 2);
            animator.SetFloat("Throttle", currentThrottle * 2);
        }

        protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
    }
}