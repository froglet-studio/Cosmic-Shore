using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using System.Linq;
namespace CosmicShore.Gameplay
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

        [Tooltip("How much of the sword's own velocity at the contact point reaches the prism. 1 = the physical model: a tip strike mid-swipe drives debris many times harder than a hilt graze, and along the swing tangent. 0 = vessel velocity only (pre-model behaviour). Requires a SkimmerSwingKinematics on the skimmer.")]
        [SerializeField] private float swingVelocityScale = 1f;

        [Tooltip("Ceiling on the impact speed handed to the prism. 0 = unclamped.")]
        [SerializeField] private float maxImpactSpeed;

        [Tooltip("ON: debris leaves at the actual impact speed, identically for every prism size - a tip strike visibly throws mass harder than a hilt graze. OFF (legacy): debris speed is impact * inertia / prismVolume, a gain spanning ~100x across prism sizes that the explosion clamp then flattens.")]
        [SerializeField] private bool proportionalDebris;

        [Tooltip("Debris speed as a multiple of impact speed. 1 = the prism leaves at the speed of the thing that hit it.")]
        [SerializeField] private float restitution = 1f;

        [Tooltip("Ceiling on debris speed, in real speed units, replacing the explosion prefab's clamp.")]
        [SerializeField] private float debrisSpeedLimit = 600f;

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

            // Otherwise: normal damage flow, carrying the velocity of the part of the sword
            // that actually made contact (see SkimmerSwingKinematics) rather than the hull's.
            var velocity = PrismEffectHelper.ContactVelocity(
                impactor, status, prismImpactee.Prism.transform.position, swingVelocityScale, maxImpactSpeed);

            if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, velocity, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);
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
                bounceDir   = Vector3.Reflect(incomingDir, normal); // unit in, unit normal -> unit out
            }
            else
            {
                // Simple "go back the way you came"
                bounceDir = -incomingDir; // incomingDir already unit
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