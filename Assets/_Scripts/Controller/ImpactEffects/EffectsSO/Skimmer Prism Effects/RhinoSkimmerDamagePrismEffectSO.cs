using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino energy-sword skimmer vs prism effect (RHINO_ENERGY_SWORD.md). The sword is UNGATED —
    /// no stance, no cooldown, no energize requirement:
    /// <list type="bullet">
    /// <item>Normal / shielded prism — standard damage on contact (a shielded prism loses its
    /// shield, a normal prism explodes), exactly like the generic skimmer damage effect.</item>
    /// <item>Super-shielded prism — POPPED on contact via the sanctioned mass-conserving teardown
    /// (DeactivateShields → devastating Damage, the AstroLeagueArena.ClearEdgeLining precedent).
    /// Set <see cref="destroySuperShielded"/> false to restore the legacy bounce instead.</item>
    /// </list>
    /// Every prism the sword actually destroys banks energy on the per-vessel
    /// <see cref="IRhinoSwordState"/> (read via <c>impactor.Skimmer.SwordState</c>) and kicks its
    /// impact-flash feedback. With no sword state present (a non-Rhino skimmer reusing this asset)
    /// the damage/pop behavior is identical — only the energy/FX bookkeeping is skipped.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/RhinoSkimmerDamagePrismEffectSO")]
    public sealed class RhinoSkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Damage")]
        [SerializeField] private float inertia = 70f;

        [Header("Super-shielded prisms")]
        [Tooltip("True (the sword's whole point): pop super-shielded prisms on contact — stellation " +
                 "shatter + devastating explode-out. False: legacy bounce-off behavior.")]
        [SerializeField] private bool destroySuperShielded = true;

        [Header("Energy banked per prism destroyed (normalized 0..1)")]
        [Tooltip("Energy the sword banks per prism it destroys. ~1/energyPerPrism kills fill the " +
                 "meter, powering a full-size crystal burst.")]
        [SerializeField] private float energyPerPrism = 0.04f;
        [Tooltip("Energy banked for popping a super-shielded prism (worth more — they are the " +
                 "hardened targets the sword exists to cut).")]
        [SerializeField] private float energyPerSuperShieldedPrism = 0.12f;

        [Header("Bounce (super-shield, only when destroySuperShielded is off)")]
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

            var prism = prismImpactee.Prism;
            if (prism == null || prism.destroyed) return;

            var sword = impactor.Skimmer.SwordState; // null on any non-energy-sword skimmer

            if (IsSuperShield(prismImpactee))
            {
                if (destroySuperShielded)
                {
                    PopSuperShield(status, prism);
                    sword?.AddEnergy(energyPerSuperShieldedPrism);
                    sword?.NotifyPrismDestroyed(superShielded: true);
                }
                else
                {
                    BounceBack(status, prismImpactee);
                }
                return;
            }

            PrismEffectHelper.Damage(status, prismImpactee, inertia, status.Course, status.Speed);

            // A shielded prism survives the hit (its shield pops instead) — bank energy and flash
            // only when this hit actually destroyed the prism.
            if (prism.destroyed && sword != null)
            {
                sword.AddEnergy(energyPerPrism);
                sword.NotifyPrismDestroyed(superShielded: false);
            }
        }

        // Sanctioned mass-conserving super-shield teardown (the AstroLeagueArena.ClearEdgeLining
        // precedent): drop the shields first — Damage() hard-ignores super-shielded prisms — so the
        // stellation shatter plays and the canonical animated Damage explode-out can run, then
        // devastate so the prism cannot restore.
        void PopSuperShield(IVesselStatus status, Prism prism)
        {
            prism.DeactivateShields(); // synchronously clears IsSuperShielded; plays shatter + SFX
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

        /// <summary>Super-shield detection: the prism's current block state.</summary>
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
