using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino energy-sword skimmer vs prism effect (RHINO_ENERGY_SWORD.md). Gated by the sword's
    /// per-vessel <see cref="IRhinoSwordState"/> (read via <c>impactor.Skimmer.SwordState</c>):
    /// <list type="bullet">
    /// <item>Normal prism — damaged only when the sword <c>CanSlashDamage</c> (a slash is being
    /// pulled and the 1s cooldown has elapsed) OR the blade is energized. Each hit lands a slash
    /// (starting the cooldown) and gains energy.</item>
    /// <item>Super-shielded prism — POPPED when energized (DeactivateShields → devastating Damage,
    /// the sanctioned mass-conserving teardown); otherwise the Rhino bounces off as before.</item>
    /// </list>
    /// With no sword state present (any non-Rhino skimmer that reuses this asset) it falls back to
    /// the legacy behavior: always damage normal prisms, always bounce super-shields.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/RhinoSkimmerDamagePrismEffectSO")]
    public sealed class RhinoSkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Damage (when NOT super-shield)")]
        [SerializeField] private float inertia = 70f;

        [Header("Energy gained per prism destroyed (normalized 0..1)")]
        [Tooltip("Energy the sword banks each time a slash destroys a prism. ~1/energyPerPrism " +
                 "prism kills fill the meter, enabling a full-power crystal burst.")]
        [SerializeField] private float energyPerPrism = 0.04f;

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

            var sword = impactor.Skimmer.SwordState; // null on any non-energy-sword skimmer

            // Super-shield: only an ENERGIZED blade shatters it; otherwise the Rhino bounces off.
            if (IsSuperShield(prismImpactee))
            {
                if (sword != null && sword.IsEnergized)
                {
                    PopSuperShield(status, prismImpactee);
                    AfterSlashLands(sword);
                }
                else
                {
                    BounceBack(status, prismImpactee);
                }
                return;
            }

            // Normal prism: while an energy sword is present, damage only during an allowed slash
            // (energized ignores the cooldown). No sword state -> legacy always-damage behavior.
            if (sword != null && !sword.IsEnergized && !sword.CanSlashDamage) return;

            PrismEffectHelper.Damage(status, prismImpactee, inertia, status.Course, status.Speed);
            AfterSlashLands(sword);
        }

        // A slash landed damage: start the slash cooldown and bank energy for destroying a prism.
        void AfterSlashLands(IRhinoSwordState sword)
        {
            if (sword == null) return;
            sword.NotifySlashLanded();
            sword.AddEnergy(energyPerPrism);
        }

        // Sanctioned mass-conserving super-shield teardown (see AstroLeagueArena.ClearEdgeLining):
        // drop the shields first so the animated Damage explode-out can run, then devastate.
        void PopSuperShield(IVesselStatus status, PrismImpactor prismImpactee)
        {
            var prism = prismImpactee.Prism;
            if (prism == null) return;

            prism.DeactivateShields(); // synchronously clears IsSuperShielded -> Normal
            var damage = status.Course * status.Speed * inertia;
            prism.Damage(damage, status.Domain, status.PlayerName, devastate: true);
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