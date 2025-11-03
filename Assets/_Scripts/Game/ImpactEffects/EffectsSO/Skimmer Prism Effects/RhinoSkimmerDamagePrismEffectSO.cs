using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Rhino-specific skimmer vs prism effect:
    /// - If the impacted prism is "Super Shield", the Rhino bounces back (no damage).
    /// - Otherwise, applies standard damage: inertia * speed * course.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/RhinoSkimmerDamagePrismEffectSO")]
    public sealed class RhinoSkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Damage (when NOT super-shield)")]
        [SerializeField] private float inertia = 70f;

        [Header("Bounce (when super-shield)")]
        [Tooltip("Multiplier applied to current speed to compute bounce target speed.")]
        [SerializeField] private float bounceSpeedMultiplier = 0.85f;

        [Tooltip("Minimum absolute speed after bounce to ensure a visible recoil.")]
        [SerializeField] private float minBounceSpeed = 10f;

        [Tooltip("How quickly we push the velocity towards the bounce vector (deltaV * dt * accelScale).")]
        [SerializeField] private float accelScale = 20f;

        [Tooltip("If true, reflect against the prism's orientation; if false, just reverse the incoming course.")]
        [SerializeField] private bool usePrismNormalReflection = false;

        [Tooltip("Extra yaw tilt applied during bounce to sell the impact.")]
        [SerializeField] private float spinStrength01 = 1f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            if (impactor == null || impactor.Skimmer == null || prismImpactee == null) return;

            var status = impactor.Skimmer.VesselStatus;
            if (status == null || status.ShipTransform == null) return;

            // Branch: Super-shield => bounce & exit
            if (IsSuperShield(prismImpactee))
            {
                BounceBack(status, prismImpactee);
                return;
            }

            // Otherwise: normal damage flow
            PrismEffectHelper.Damage(status, prismImpactee, inertia, status.Course, status.Speed);
        }

        private void BounceBack(IVesselStatus status, PrismImpactor prismImpactee)
        {
            // Current kinematics
            var course = status.Course;
            var speed  = Mathf.Max(0f, status.Speed);

            Vector3 incomingDir = course.sqrMagnitude > 0.0001f
                ? course.normalized
                : status.ShipTransform.forward;

            Vector3 bounceDir;
            if (usePrismNormalReflection)
            {
                // Use prism forward (with a tiny tilt via cross to avoid degenerate parallel cases)
                var prismTf = prismImpactee.Prism.prismProperties.prism.transform;
                var cross   = Vector3.Cross(incomingDir, prismTf.forward);
                var normal  = Quaternion.AngleAxis(15f, cross) * prismTf.forward;
                bounceDir   = Vector3.Reflect(incomingDir, normal).normalized;
            }
            else
            {
                // Simple "go back the way you came"
                bounceDir = (-incomingDir).normalized;
            }

            float targetSpeed = Mathf.Max(speed * bounceSpeedMultiplier, minBounceSpeed);

            // Compute delta-V needed to switch from current velocity to desired bounced velocity
            Vector3 currentVel = incomingDir * speed;
            Vector3 desiredVel = bounceDir * targetSpeed;
            Vector3 deltaV     = desiredVel - currentVel;

            // Nudge velocity towards the bounce target
            status.VesselTransformer.ModifyVelocity(deltaV, Time.deltaTime * accelScale);

            // Give the ship a quick, gentle spin towards the new heading (keeps roll natural)
            var up         = status.ShipTransform.up;
            var right      = Vector3.Cross(up, bounceDir).normalized;
            var correctedUp= Vector3.Cross(bounceDir, right).normalized;

            status.VesselTransformer.GentleSpinShip(bounceDir, correctedUp, Mathf.Clamp01(spinStrength01));
        }

        /// <summary>
        /// Determines whether the impacted prism is a "Super Shield" prism.
        /// Convention: add a SuperShieldPrismTag component on the prism root (or the object referenced by prismProperties.prism).
        /// </summary>
        private static bool IsSuperShield(PrismImpactor prismImpactee)
        {
            if (prismImpactee?.Prism == null || prismImpactee.Prism.prismProperties == null)
                return false;

            var prismRoot = prismImpactee.Prism.prismProperties.prism;
            if (prismRoot == null) return false;

            return prismRoot.CurrentState == BlockState.SuperShielded;
        }
    }
}