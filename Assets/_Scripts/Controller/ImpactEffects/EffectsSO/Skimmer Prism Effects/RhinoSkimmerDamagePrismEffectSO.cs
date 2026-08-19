using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino energy-sword skimmer vs prism effect (RHINO_ENERGY_SWORD.md). Ordinary cutting is
    /// UNGATED — no stance, no cooldown; the ONE gated act is the super-shield pop:
    /// <list type="bullet">
    /// <item>Normal / shielded prism — standard damage on contact (a shielded prism loses its
    /// shield, a normal prism explodes), exactly like the generic skimmer damage effect.</item>
    /// <item>Super-shielded prism — POPPED only while the blade is ENERGIZED
    /// (<see cref="IRhinoSwordState.IsEnergized"/>, the hold-the-chop-stance ritual), via the
    /// sanctioned mass-conserving teardown (DeactivateShields → devastating Damage, the
    /// AstroLeagueArena.ClearEdgeLining precedent). A non-energized blade recoils
    /// (<see cref="BounceBack"/>) with a dim denied-spark. Set
    /// <see cref="popRequiresEnergizedBlade"/> false to pop ungated (the v2 debug behavior).</item>
    /// </list>
    /// Every prism the sword actually destroys banks energy on the per-vessel
    /// <see cref="IRhinoSwordState"/> (read via <c>impactor.Skimmer.SwordState</c>) and kicks its
    /// impact feedback (blade flash + contact spark). With no sword state present (a non-Rhino
    /// skimmer reusing this asset) the blade is never energized, so super-shielded prisms always
    /// bounce; the ordinary damage behavior is identical — only the energy/FX bookkeeping is
    /// skipped.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/RhinoSkimmerDamagePrismEffectSO")]
    public sealed class RhinoSkimmerDamagePrismEffectSO : SkimmerPrismEffectSO
    {
        [Header("Damage")]
        [SerializeField] private float inertia = 70f;

        [Tooltip("How much of the sword's own velocity at the contact point reaches the prism. 1 = the physical model: a tip strike mid-swipe drives debris many times harder than a hilt graze, and along the swing tangent. 0 = vessel velocity only (pre-model behaviour). Requires a SkimmerSwingKinematics on the skimmer.")]
        [SerializeField] private float swingVelocityScale = 1f;

        [Tooltip("Ceiling on the impact speed handed to the prism. 0 = unclamped.")]
        [SerializeField] private float maxImpactSpeed;

        [Tooltip("ON: debris leaves at the actual impact speed, identically for every prism size - a tip strike visibly throws mass harder than a hilt graze. OFF (legacy): debris speed is impact * inertia / prismVolume, a gain spanning ~100x across prism sizes that the explosion clamp then flattens.")]
        [SerializeField] private bool proportionalDebris;

        [Tooltip("Debris speed as a multiple of impact speed. 1 = the physical read (the prism leaves at the speed of the thing that hit it); shipped at 1/3 because full speed reads too hot. Also drives the shatter RATE, so gentle hits crumble slowly and hard hits burst instantly - scale it and both scale together.")]
        [SerializeField] private float restitution = 1f / 3f;

        [Tooltip("Ceiling on debris speed, in real speed units, replacing the explosion prefab's clamp.")]
        [SerializeField] private float debrisSpeedLimit = 200f;

        [Header("Super-shielded prisms")]
        [Tooltip("True (v3, the energize ritual): popping a super-shielded prism requires the blade " +
                 "to be ENERGIZED — hold the both-triggers chop stance to charge it. A non-energized " +
                 "blade bounces off. False: the blade pops super-shields ungated (the v2 behavior, " +
                 "kept as a designer A/B switch).")]
        [SerializeField] private bool popRequiresEnergizedBlade = true;

        [Header("Energy banked per prism destroyed (normalized 0..1)")]
        [Tooltip("Energy the sword banks per prism it destroys. ~1/energyPerPrism kills fill the " +
                 "meter, powering a full-size crystal burst.")]
        [SerializeField] private float energyPerPrism = 0.04f;
        [Tooltip("Energy banked for popping a super-shielded prism (worth more — they are the " +
                 "hardened targets the sword exists to cut).")]
        [SerializeField] private float energyPerSuperShieldedPrism = 0.12f;

        [Header("Bounce (super-shield contact while the blade is NOT energized)")]
        [Tooltip("Multiplier applied to current speed to compute bounce target speed.")]
        [SerializeField] private float bounceSpeedMultiplier = 0.85f;

        [Tooltip("Minimum absolute speed after bounce to ensure a visible recoil.")]
        [SerializeField] private float minBounceSpeed = 10f;

        [Tooltip("Seconds the recoil delta-V is applied over — a FIXED window, so the shove is " +
                 "frame-rate independent (the old deltaV * dt * accelScale form handed ModifyVelocity " +
                 "a frame-time-scaled DURATION: a 30 fps player got ~4x the recoil of a 120 fps one).")]
        [SerializeField] private float bounceDurationSeconds = 0.35f;

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

            // The velocity of the part of the sword that actually made contact (see
            // SkimmerSwingKinematics) rather than the hull's — a tip strike mid-swipe throws mass
            // far harder than a hilt graze, along the swing tangent. Collapses to Course * Speed
            // on any skimmer with no swing model. Computed once: the super-shield pop below
            // scatters its shell with the same velocity the normal path uses.
            var velocity = PrismEffectHelper.ContactVelocity(
                impactor, status, prism.transform.position, swingVelocityScale, maxImpactSpeed);

            if (IsSuperShield(prismImpactee))
            {
                // The supershield key: only an ENERGIZED blade pops hardened mass. With no
                // sword state (non-Rhino skimmer reusing the asset) the blade can never be
                // energized, so the contact recoils — the pre-sword baseline behavior.
                bool energized = sword is { IsEnergized: true };
                if (popRequiresEnergizedBlade && !energized)
                {
                    sword?.NotifyPopDenied(prism.transform.position);
                    BounceBack(status, prismImpactee);
                    return;
                }

                PopSuperShield(status, prism, velocity);
                sword?.AddEnergy(energyPerSuperShieldedPrism);
                sword?.NotifyPrismDestroyed(superShielded: true, prism.transform.position);
                return;
            }

            if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, velocity, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);

            // A shielded prism survives the hit (its shield pops instead) — bank energy and flash
            // only when this hit actually destroyed the prism.
            if (prism.destroyed && sword != null)
            {
                sword.AddEnergy(energyPerPrism);
                sword.NotifyPrismDestroyed(superShielded: false, prism.transform.position);
            }
        }

        // Sanctioned mass-conserving super-shield teardown (the AstroLeagueArena.ClearEdgeLining
        // precedent): drop the shields first — Damage() hard-ignores super-shielded prisms — so the
        // stellation shatter plays and the canonical animated Damage explode-out can run, then
        // devastate so the prism cannot restore. Debris carries the contact velocity on the same
        // terms as the normal path (PrismEffectHelper.DamageProportional can't devastate, so the
        // proportional branch reproduces its two lines here rather than forking the helper).
        void PopSuperShield(IVesselStatus status, Prism prism, Vector3 contactVelocity)
        {
            // Synchronously clears IsSuperShielded; plays shatter + SFX. The contact velocity
            // goes in so the stellation's shards blow off along the SWING (the point of the
            // blade that touched, per SkimmerSwingKinematics) rather than puffing outward —
            // the same vector the devastating Damage below hands the debris.
            prism.DeactivateShields(contactVelocity);

            if (proportionalDebris)
            {
                prism.Damage(contactVelocity * restitution, status.Domain, status.PlayerName,
                             devastate: true, debrisSpeedLimit: debrisSpeedLimit);
                return;
            }

            prism.Damage(contactVelocity * inertia, status.Domain, status.PlayerName, devastate: true);
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

            // Apply the recoil over a fixed window (frame-rate independent — this dispatch
            // fires ONCE per contact entry, so nothing accumulates across frames).
            status.VesselTransformer.ModifyVelocity(deltaV, bounceDurationSeconds);

            // Give the ship a quick, gentle spin towards the new heading (keeps roll natural)
            var up         = status.ShipTransform.up;
            var right      = Vector3.Cross(up, bounceDir).normalized;
            var correctedUp= Vector3.Cross(bounceDir, right).normalized;

            status.VesselTransformer.GentleSpinShip(bounceDir, correctedUp, Mathf.Clamp01(spinStrength01));
        }

        /// <summary>Super-shield detection: the CANONICAL invulnerability flag — the same one
        /// Prism.Damage/Consume early-return on and the shell tier gates contact ownership with
        /// (PrismShellContactManager.ShellOwnsContact). NOT PrismStateManager.CurrentState:
        /// SegmentSpawner's track super-shielding deliberately sets the flag while leaving the
        /// legacy state machine at Normal (so state-keying missed every Skim-Race/HexRace track
        /// prism — no pop, no bounce, no denied feedback), and Prism.ResetState clears the flag
        /// on pool reuse without resetting CurrentState (so state-keying bounced ordinary reborn
        /// mass, violating the ungated-cutting contract, and over-banked its kills).</summary>
        private static bool IsSuperShield(PrismImpactor prismImpactee)
        {
            var prism = prismImpactee?.Prism;
            if (prism == null) return false;
            return prism.prismProperties is { IsSuperShielded: true };
        }
    }
}
