using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's spikes — the asset behind BOTH of its firing abilities, authored twice:
    ///
    /// * **Spike Volley** (SPACE, right stick) — an aimed burst that costs ammo and chains.
    /// * **Spike Barrage** (CHARGE, left stick) — free spikes in every direction, which at base
    ///   steal but do not chain, and which the CHARGE level-5 upgrade turns into a cascade too.
    ///
    /// One SO type for both because they are one weapon with two patterns. A spike does the
    /// same three things wherever it lands (embed, steal, fire the next generation), and its
    /// two element dials apply to both abilities: SPACE is how far every spike reaches, CHARGE
    /// is how deep every cascade runs. That keeps the fleet's one-parameter-per-element
    /// convention intact instead of giving each ability its own private copy of the other's dial.
    ///
    /// The asset is SHARED by every Urchin in a match and holds no per-vessel state; everything
    /// live is resolved from the passed <see cref="IVesselStatus"/> at fire time.
    /// </summary>
    [CreateAssetMenu(fileName = "UrchinSpikeAction",
        menuName = "ScriptableObjects/Vessel Actions/Urchin Spike")]
    public class UrchinSpikeActionSO : ShipActionSO
    {
        [Header("Pattern")]
        [Tooltip("Default = an aimed volley out of the muzzles (Spike Volley). " +
                 "Spherical = spikes in every direction from the hull (Spike Barrage).")]
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Default;

        [Tooltip("Hold to keep firing, or fire once per press. The barrage is a one-shot burst; " +
                 "the volley repeats while held.")]
        [SerializeField] bool repeatWhileHeld = true;

        [Header("Pattern shape")]
        [Tooltip("Spherical only: total spikes in the SHIP'S OWN volley (the golden-spiral " +
                 "count). The hull historically carried 18 authored ShootPoint ports (mirrored " +
                 "~36 across the ship); the spiral supersedes them with an even sphere and no " +
                 "gaps. Chain children keep their budgeted energy-derived counts. 0 = legacy " +
                 "energy-derived count for the ship volley too.")]
        [SerializeField, Range(0, 64)] int barrageSpikeCount = 36;

        [Tooltip("ConcentricRings only: rings per blast. Ring r sits at coneHalfAngle*r/rings " +
                 "and carries spikesPerRing*r spikes, alternate rings staggered to fill gaps.")]
        [SerializeField, Range(1, 5)] int ringCount = 3;

        [Tooltip("ConcentricRings only: spikes in the innermost ring (outer rings scale up). " +
                 "Authored PER MUZZLE - the blast fires one fan from every muzzle, each spun " +
                 "half a spoke off the last, so a two-gun vessel throws twice this and the two " +
                 "fans interleave rather than overlap.")]
        [SerializeField, Range(2, 12)] int spikesPerRing = 3;

        [Tooltip("ConcentricRings only: cone half-angle of the OUTERMOST ring, degrees. Small " +
                 "is the point - this is a shotgun aimed down the nose, not a spray.")]
        [SerializeField, Range(2f, 80f)] float coneHalfAngleDegrees = 9f;

        [Tooltip("ConcentricRings only: also fire one spike straight down the aim axis.")]
        [SerializeField] bool centerSpike = true;

        [Header("Cost")]
        [SerializeField] int ammoIndex = 0;

        [Tooltip("Ammo per volley. The barrage is authored FREE (0) - 'fire free spikes in all " +
                 "directions' - and is paid for instead by its shallower cascade.")]
        [SerializeField] float ammoCost = 0.15f;

        [Header("Flight")]
        [SerializeField] float firingRate = 3f;
        [SerializeField] float projectileSpeed = 60f;
        [SerializeField] float projectileTime = 2f;
        [SerializeField] float projectileScale = 1f;

        [Header("Chain reaction")]
        [Tooltip("Generations the cascade may run at RESTING Charge (level 0). 0 = spikes " +
                 "steal but never chain. Each generation fires 2*(g+3) children, so this " +
                 "escalates fast: 1 -> 8 spikes, 2 -> 10 then 8 each, 3 -> 12 then 10 then 8.")]
        [SerializeField, Range(0, 4)] int generationsAtRestingCharge = 1;

        [Tooltip("The same at Charge level 10. Linear in LEVEL and extrapolated across the " +
                 "element system's full [-5, 15] band, then clamped to the 0..4 the pool tiers " +
                 "and the frame budget can actually carry.")]
        [SerializeField, Range(0, 4)] int generationsAtFullCharge = 3;

        [Tooltip("Fraction of its reach each generation hands to the next, so a deep cascade " +
                 "visibly runs out of steam. The SPACE level-5 upgrade ('Deep Cascade') " +
                 "overrides this to 1.")]
        [SerializeField, Range(0.05f, 1f)] float generationRangeFalloff = 0.75f;

        [Tooltip("CHARGE level-5 'Overcharge': grant this ability ONE EXTRA generation. Not a " +
                 "floor-at-one - the upgrade unlocks at Charge 5, where the depth curve already " +
                 "returns at least 1 for every authored pair, so a floor could never bind and " +
                 "the upgrade did nothing. It is the barrage's whole upgrade.")]
        [SerializeField] bool chainsOnChargeUpgrade;

        public FiringPatterns FiringPattern => firingPattern;
        public bool RepeatWhileHeld => repeatWhileHeld;
        public int AmmoIndex => ammoIndex;
        public float AmmoCost => ammoCost;
        public float FiringRate => firingRate;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTime => projectileTime;
        public float ProjectileScale => projectileScale;
        public int BarrageSpikeCount => barrageSpikeCount;
        public int RingCount => ringCount;
        public int SpikesPerRing => spikesPerRing;
        public float ConeHalfAngleDegrees => coneHalfAngleDegrees;
        public bool CenterSpike => centerSpike;

        /// <summary>
        /// SPACE -> reach. The authored muzzle speed scaled by the vessel's live Space
        /// multiplier from its <c>ElementalAbilityMapSO</c>. Read per volley at fire time,
        /// never cached across a hold and never bound as an <c>ElementalFloat</c> on this
        /// shared asset (per-vessel state on a shared SO is last-initializer-wins).
        /// </summary>
        public float ResolveRangeScale(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return abilities ? abilities.Multiplier(Element.Space) : 1f;
        }

        /// <summary>
        /// Per-generation reach decay, or 1 when the pilot holds the SPACE level-5 upgrade
        /// ("Deep Cascade") and the wavefront keeps its full reach to the last generation.
        ///
        /// Gated on <c>IsUpgradeActive</c> — the replicated unlock bit — and NOT on a raw local
        /// level read, because this changes the prismscape and a local read desyncs it.
        /// </summary>
        public float ResolveRangeFalloff(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            bool deepCascade = abilities != null && abilities.IsUpgradeActive(Element.Space);
            return deepCascade ? 1f : generationRangeFalloff;
        }

        /// <summary>
        /// CHARGE -> depth. How many generations this volley's spikes may propagate, from the
        /// vessel's LIVE Charge level, plus the level-5 "Overcharge" bonus generation.
        ///
        /// KNOWN GAP (multiplayer): the level read is the LOCAL ResourceSystem's, which does
        /// not replicate - unlike the unlock BIT that ResolveRangeFalloff correctly uses. Depth
        /// changes the prismscape, so peers can run different-depth cascades until a replicated
        /// level surface exists (Docs/ElementalAbilitySystem/ARCHITECTURE.md 3.4).
        /// </summary>
        public int ResolveGenerations(IVesselStatus status)
        {
            var resources = status?.ResourceSystem;
            int level = resources ? resources.GetLevel(Element.Charge) : 0;
            int generations = GenerationsForLevel(level, generationsAtRestingCharge, generationsAtFullCharge);

            // "Overcharge" ADDS a generation; it does not floor at one. A floor was a no-op by
            // construction: the upgrade unlocks at Charge 5, and at Charge 5 the depth curve
            // already returns >= 1 for every authored pair the assets use, so Mathf.Max(g, 1)
            // could never bind and the level-5 upgrade did literally nothing. Clamped to the
            // same [0, 4] ceiling GenerationsForLevel enforces, so the pool tiers and the
            // per-frame volley budget still bound the worst case.
            var abilities = status?.ElementalAbilityHandler;
            if (chainsOnChargeUpgrade && abilities != null && abilities.IsUpgradeActive(Element.Charge))
                generations = Mathf.Clamp(generations + 1, 0, 4);

            return generations;
        }

        /// <summary>
        /// The depth curve, pulled out as a pure function so it is edit-mode testable. Linear
        /// in level with the authored pair anchored at levels 0 and 10 and extrapolated across
        /// [-5, 15], then clamped to [0, 4] — the range the projectile pool tiers and the
        /// per-frame volley budget are sized for.
        /// </summary>
        public static int GenerationsForLevel(int chargeLevel, int atResting, int atFull)
            => Mathf.Clamp(
                   Mathf.RoundToInt(Mathf.LerpUnclamped(atResting, atFull, chargeLevel / 10f)),
                   0, 4);

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<UrchinSpikeActionExecutor>()?.Begin(this);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<UrchinSpikeActionExecutor>()?.End(this);
    }
}
