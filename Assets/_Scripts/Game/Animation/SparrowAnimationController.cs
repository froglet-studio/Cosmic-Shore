using UnityEngine;

namespace CosmicShore.Game.Animation
{
    class SparrowAnimationController : VesselAnimation
    {
        [SerializeField] Animator animator;
        [SerializeField] FireGunActionExecutor missileExecutor;

        float currentPitch = 0;
        float currentYaw = 0;
        float currentRoll = 0;
        float currentThrottle = 0;
        float animationSpeed = 3.25f;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (missileExecutor != null)
                missileExecutor.OnMissileFired += HandleMissileFired;
        }

        void OnDestroy()
        {
            if (missileExecutor != null)
                missileExecutor.OnMissileFired -= HandleMissileFired;
        }

        void HandleMissileFired(float ammoBeforeFire, float ammoCost)
        {
            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missle Launch 1" : "Missle Launch 2";
            animator.Play(animName);
        }

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

        protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
    }
}