using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino sword skimmer vs prism effect (RHINO_ENERGY_SWORD.md). Ordinary cutting is
    /// UNGATED — no stance, no cooldown; the ONE gated act is the super-shield pop:
    /// <list type="bullet">
    /// <item>Normal / shielded prism — standard damage on contact (a shielded prism loses its
    /// shield, a normal prism explodes), exactly like the generic skimmer damage effect.</item>
    /// <item>Super-shielded prism — POPPED only under the SPACE level-5 upgrade
    /// (<see cref="superShieldUpgradeElement"/>, Garrett 2026-10-26: "the sword only destroys
    /// supershielded prisms as the space L5 upgrade") AND while the blade is ENERGIZED
    /// (<see cref="IRhinoSwordState.IsEnergized"/>, the hold-the-chop-stance ritual), via the
    /// sanctioned mass-conserving teardown (DeactivateShields → devastating Damage, the
    /// AstroLeagueArena.ClearEdgeLining precedent). A non-energized blade BINDS in it rather
    /// than recoiling: this effect plays the entry beat (a dim denied-spark, the deflection
    /// jiggle, the punish thud), and <see cref="ShieldSkimmerScaleDriver"/> owns the rest for as
    /// long as the blade stays inside — slowed turning, a continued shudder, a haptic grind
    /// (RHINO_ENERGY_SWORD.md § "Binding"). A blade without the upgrade binds the same way,
    /// energized or not. The old <see cref="BounceBack"/> recoil threw the
    /// ship around and is kept only behind <see cref="recoilWhenDenied"/> as an A/B switch. Set
    /// <see cref="popRequiresEnergizedBlade"/> false to pop ungated (the v2 debug behavior).</item>
    /// </list>
    /// Every prism the sword actually destroys kicks its impact feedback (blade flash + contact
    /// spark) on the per-vessel <see cref="IRhinoSwordState"/> (read via
    /// <c>impactor.Skimmer.SwordState</c>). It banks NOTHING: the Rhino's energy was discarded on
    /// 2026-10-26, along with the Charge-scaled gain that fed it (Charge now scales the sword's
    /// petal STEAL, <c>RhinoSwordStealBySkimmerEffect</c>). The sword's own AFTERIMAGES (its Mass
    /// ability) are prisms like any other: it cuts them (Garrett, 2026-10-17: "not immune to our
    /// destruction"). Only the fleet's 1 s self-trail grace keeps it off one it has just laid.
    /// With no sword state present (a non-Rhino skimmer reusing this asset) the blade is never
    /// energized, so super-shielded prisms always bind; the ordinary damage behavior is identical -
    /// only the FX bookkeeping is skipped.
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

        [Header("Slice (the death visual)")]
        [Tooltip("ON: a prism the blade destroys is CUT — split along the plane the blade swept " +
                 "through it, the halves parting, opening and dissolving from the cut face " +
                 "(Docs/PRISM_ANIMATION.md §4.10, RHINO_ENERGY_SWORD.md § \"The slice\"). OFF: the " +
                 "ordinary prism explosion. Photons only — damage, stats and SFX are identical " +
                 "either way, and a slice the budget cannot afford falls back to the explosion.")]
        [SerializeField] private bool sliceDestroyedPrisms = true;

        [Header("Super-shielded prisms")]
        [Tooltip("Element whose level-5 upgrade lets the sword destroy SUPER-SHIELDED prisms at all " +
                 "(the Rhino: Space). Without it the blade binds in hardened mass, energized or not. " +
                 "Gated on the replicated unlock bit (IsUpgradeActive), snapshotted per hit - whether " +
                 "a prism survives is an outcome every peer has to agree on. None = no upgrade needed " +
                 "(the energize alone is the key, the pre-2026-10-26 behaviour).")]
        [SerializeField] private Element superShieldUpgradeElement = Element.Space;

        [Tooltip("True (v3, the energize ritual): popping a super-shielded prism requires the blade " +
                 "to be ENERGIZED — hold the both-triggers chop stance to charge it. A non-energized " +
                 "blade binds in it (see Denied contact). False: the blade pops super-shields ungated (the v2 behavior, " +
                 "kept as a designer A/B switch).")]
        [SerializeField] private bool popRequiresEnergizedBlade = true;

        [Header("Denied contact (super-shield touched while the blade is NOT energized)")]
        [Tooltip("OFF (shipped): the blade BINDS — no velocity change, no spin; the prism jiggles, the " +
                 "pilot feels a thud, and ShieldSkimmerScaleDriver slows the vessel's turn for as long " +
                 "as the blade stays inside. ON (legacy A/B): the old recoil below throws the ship back " +
                 "along its course and spins it.")]
        [SerializeField] private bool recoilWhenDenied;

        [Header("Legacy recoil (only while recoilWhenDenied is ON)")]
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

            // A SILENCED pilot's blade still swings and still connects, but cuts no rival mass,
            // pops no shield and banks no energy for it (VesselSilence). Asked first, because the
            // shield pops below act before any Damage call could refuse them.
            if (prism.DeflectsSilenced(status.PlayerName, velocity.magnitude)) return;

            if (IsSuperShield(prismImpactee))
            {
                // The supershield key: only an UPGRADED (Space L5), ENERGIZED blade pops hardened
                // mass. With no sword state (non-Rhino skimmer reusing the asset) the blade can
                // never be energized, so the contact binds — the pre-sword baseline behavior.
                bool energized = sword is { IsEnergized: true };
                if (!SuperShieldUpgradeActive(status) || (popRequiresEnergizedBlade && !energized))
                {
                    sword?.NotifyPopDenied(prism.transform.position);
                    if (recoilWhenDenied)
                    {
                        BounceBack(status, prismImpactee);
                        return;
                    }

                    // The ENTRY beat of a bind. The jiggle is photons only — the prism stays
                    // invulnerable (Prism.AbsorbSuperShieldHit is the one super-shield gate), and
                    // it is sized by the speed the blade actually struck at. Everything after
                    // this frame — the drag, the continued shudder, the grind — is the sword
                    // driver's, because this effect fires once per contact entry.
                    prism.AbsorbSuperShieldHit(velocity.magnitude);
                    if (status.IsLocalUser && !status.AutoPilotEnabled)
                        HapticController.PlayPunish();
                    return;
                }

                PopSuperShield(impactor, status, prism, velocity);
                sword?.NotifyPrismDestroyed(superShielded: true, prism.transform.position);
                return;
            }

            if (TryResolveCut(impactor, status, prism.transform.position, velocity, out var cutPoint, out var cutNormal))
            {
                // The same impact vector and ceiling either branch below would hand Damage; only
                // the death's picture changes.
                if (proportionalDebris)
                    prism.Slice(velocity * restitution, status.Domain, status.PlayerName, cutPoint, cutNormal,
                                debrisSpeedLimit: debrisSpeedLimit);
                else
                    prism.Slice(velocity * inertia, status.Domain, status.PlayerName, cutPoint, cutNormal);
            }
            else if (proportionalDebris)
                PrismEffectHelper.DamageProportional(status, prismImpactee, velocity, restitution, debrisSpeedLimit);
            else
                PrismEffectHelper.Damage(status, prismImpactee, inertia, velocity);

            // A shielded prism survives the hit (its shield pops instead) — flash only when this hit
            // actually destroyed the prism.
            if (prism.destroyed && sword != null)
                sword.NotifyPrismDestroyed(superShielded: false, prism.transform.position);
        }

        // Sanctioned mass-conserving super-shield teardown (the AstroLeagueArena.ClearEdgeLining
        // precedent): drop the shields first — Damage() hard-ignores super-shielded prisms — so the
        // stellation shatter plays and the canonical animated Damage explode-out can run, then
        // devastate so the prism cannot restore. Debris carries the contact velocity on the same
        // terms as the normal path (PrismEffectHelper.DamageProportional can't devastate, so the
        // proportional branch reproduces its two lines here rather than forking the helper).
        void PopSuperShield(SkimmerImpactor impactor, IVesselStatus status, Prism prism, Vector3 contactVelocity)
        {
            // Synchronously clears IsSuperShielded; sheds the stellation as explosion debris
            // + SFX. Each branch hands the shed the SAME vector and ceiling it hands the
            // devastating Damage below, so the shield's pieces and the prism's pieces fly on
            // identical terms — one effect, two meshes (Docs/PRISM_ANIMATION.md §4.8.1).
            ShedShield(prism, contactVelocity);

            // The hardened prism itself is CUT, like every other prism the blade kills; its
            // stellation has already shattered above, so the armour bursts and the core slices.
            bool cut = TryResolveCut(impactor, status, prism.transform.position, contactVelocity,
                                     out var cutPoint, out var cutNormal);

            if (proportionalDebris)
            {
                if (cut)
                    prism.Slice(contactVelocity * restitution, status.Domain, status.PlayerName, cutPoint, cutNormal,
                                devastate: true, debrisSpeedLimit: debrisSpeedLimit);
                else
                    prism.Damage(contactVelocity * restitution, status.Domain, status.PlayerName,
                                 devastate: true, debrisSpeedLimit: debrisSpeedLimit);
                return;
            }

            if (cut)
                prism.Slice(contactVelocity * inertia, status.Domain, status.PlayerName, cutPoint, cutNormal,
                            devastate: true);
            else
                prism.Damage(contactVelocity * inertia, status.Domain, status.PlayerName, devastate: true);
        }

        /// <summary>The level-5 upgrade that lets the sword destroy super-shielded prisms (the
        /// Rhino's Space). Through IsUpgradeActive (the replicated unlock bit), not a raw level read.</summary>
        bool SuperShieldUpgradeActive(IVesselStatus status)
        {
            if (superShieldUpgradeElement == Element.None) return true;
            var abilities = status.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(superShieldUpgradeElement);
        }

        /// <summary>Drops a prism's super-shield as explosion debris on the same terms the kill
        /// that follows hands the core, and clears the shield flag synchronously.</summary>
        void ShedShield(Prism prism, Vector3 contactVelocity)
        {
            if (proportionalDebris)
                prism.DeactivateShields(contactVelocity * restitution, debrisSpeedLimit);
            else
                prism.DeactivateShields(contactVelocity * inertia);
        }

        /// <summary>
        /// The plane the blade swept through the prism: it contains the blade's own axis and the
        /// velocity of the part of the blade that made contact, so a flat sweep cuts flat, a
        /// vertical chop cuts vertically and a blade held out while the ship flies past shears the
        /// prism along the ship's path — the cut is the stroke the pilot made. It passes through the
        /// blade point nearest the prism; PrismSliceGeometry keeps it inside the prism's middle so a
        /// grazing tip still reads as a slice rather than a chip. False (the ordinary explosion)
        /// when the slice is off or the skimmer has no blade to cut with.
        /// </summary>
        bool TryResolveCut(SkimmerImpactor impactor, IVesselStatus status, Vector3 prismPosition,
                           Vector3 contactVelocity, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            if (!sliceDestroyedPrisms) return false;

            var swing = impactor.Skimmer.SwingKinematics;
            if (swing == null || !swing.IsReady) return false;

            Vector3 blade = swing.BladeAxisWorld;
            Vector3 n = Vector3.Cross(blade, contactVelocity);
            // A stab (the blade moving along its own length) sweeps no plane; cut across the hull's
            // up instead, which is how a chop along the ship's own axis would fall.
            if (n.sqrMagnitude < 1e-6f * Mathf.Max(contactVelocity.sqrMagnitude, 1e-6f))
            {
                n = Vector3.Cross(blade, status.ShipTransform.up);
                if (n.sqrMagnitude < 1e-8f) n = Vector3.Cross(blade, status.ShipTransform.right);
                if (n.sqrMagnitude < 1e-8f) return false;
            }

            normal = n.normalized;
            point = swing.ClosestBladePoint(prismPosition);
            return true;
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
        /// legacy state machine at Normal (so state-keying missed every Skim-Race/SkimRace track
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
