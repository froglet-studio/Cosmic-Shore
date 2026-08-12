using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Sparrow puppetry (identical blend-space driving to <see cref="MantaAnimationContoller"/>,
    /// which the Sparrow prefab used before this controller shipped) plus the missile-bay
    /// launch: when the skyburst executor fires, the matching bay-open clip ("Missile Launch 1"
    /// = right bay, "Missile Launch 2" = left bay - authored in SparrowModel4.fbx, played on
    /// SparrowModel1's identical rig) runs once on the additive Missile Launching layer, then
    /// the layer weight drops back to zero.
    /// </summary>
    class SparrowAnimationController : VesselAnimation
    {
        [SerializeField] Animator animator;
        [SerializeField] bool hasBoost = false;

        [Header("Missile Bay")]
        [Tooltip("The skyburst FireGunActionExecutor on this vessel. Its fire event opens the " +
                 "missile bay in step with the shot; the executor spawns the projectile at the " +
                 "same bay's bone, so the animated missile hands off to the live one.")]
        [SerializeField] FireGunActionExecutor missileExecutor;

        const int MissileLaunchingLayer = 1;
        const string RightBayLaunchState = "Missile Launch 1";
        const string LeftBayLaunchState = "Missile Launch 2";

        float currentPitch = 0;
        float currentYaw = 0;
        float currentRoll = 0;
        float currentThrottle = 0;
        float animationSpeed = 3.25f;

        CancellationTokenSource _launchResetCts;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            // Detach-first: vessel swaps re-run Initialize on live components, and the
            // teardown must not depend on who is piloting.
            if (missileExecutor != null)
            {
                missileExecutor.OnMissileFired -= HandleMissileFired;
                missileExecutor.OnMissileFired += HandleMissileFired;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (missileExecutor != null)
                missileExecutor.OnMissileFired -= HandleMissileFired;

            _launchResetCts?.Cancel();
            _launchResetCts?.Dispose();
            _launchResetCts = null;
        }

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
        {
            if (VesselStatus.IsBoosting && hasBoost) animator.SetBool("Boost", true);
            else if (hasBoost) animator.SetBool("Boost", false);

            currentPitch = Mathf.Lerp(currentPitch, pitch, animationSpeed * Time.deltaTime);
            currentYaw = Mathf.Lerp(currentYaw, yaw, animationSpeed * Time.deltaTime);
            currentRoll = Mathf.Lerp(currentRoll, roll, animationSpeed * Time.deltaTime);
            currentThrottle = Mathf.Lerp(currentThrottle, throttle, animationSpeed * Time.deltaTime);

            animator.SetFloat("Pitch", -currentPitch * 2);
            animator.SetFloat("Yaw", currentYaw * 2);
            animator.SetFloat("Roll", currentRoll * 2);
            animator.SetFloat("Throttle", currentThrottle * 2);
        }

        protected override void Idle()
        {
            if (VesselStatus.IsBoosting) animator.SetBool("Boost", true);
            else if (hasBoost) animator.SetBool("Boost", false);

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

        void HandleMissileFired(bool usedRightBay)
        {
            if (!animator) return;

            animator.SetLayerWeight(MissileLaunchingLayer, 1f);
            // 0f start time forces a restart even when a launch is already mid-swing.
            animator.Play(usedRightBay ? RightBayLaunchState : LeftBayLaunchState, MissileLaunchingLayer, 0f);

            _launchResetCts?.Cancel();
            _launchResetCts?.Dispose();
            _launchResetCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            ResetLayerWhenLaunchCompletesAsync(_launchResetCts.Token).Forget();
        }

        async UniTaskVoid ResetLayerWhenLaunchCompletesAsync(CancellationToken token)
        {
            // Wait one frame so the animator has entered the launch state before polling it.
            await UniTask.Yield(token);
            while (animator && animator.GetCurrentAnimatorStateInfo(MissileLaunchingLayer).normalizedTime < 1f)
                await UniTask.Yield(token);

            if (animator)
                animator.SetLayerWeight(MissileLaunchingLayer, 0f);
        }
    }
}
