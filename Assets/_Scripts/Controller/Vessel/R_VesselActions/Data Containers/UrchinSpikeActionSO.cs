using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Urchin's spikes — ONE ability on ONE trigger (CHARGE, right stick), fired two ways
    /// by how long the trigger is held:
    ///
    /// * **Tap** — the aimed shotgun: concentric rings of spikes out of every muzzle, one blast
    ///   per pull, paid for in ammo. Semi-automatic; the trigger fires on the press.
    /// * **Hold, then release** — the trigger CHARGES while it is down and the release throws an
    ///   omni burst in every direction from the hull, with the spike count scaled by how long it
    ///   was held. Free, because the hold IS its price.
    ///
    /// This was two separate abilities on two triggers (an aimed "Spike Volley" on SPACE and a
    /// free "Spike Barrage" on CHARGE). They were always one weapon — a spike does the same
    /// three things wherever it lands (embed, steal, fire the next generation) — so they are now
    /// one, and the freed trigger carries the Track Projector (SPACE). CHARGE owns the whole
    /// weapon: how far each spike reaches AND how deep each cascade runs, which is what the
    /// charge-up mechanic means literally.
    ///
    /// The asset is SHARED by every Urchin in a match and holds no per-vessel state; everything
    /// live is resolved from the passed <see cref="IVesselStatus"/> at fire time — including the
    /// charge, which is timed on the per-vessel EXECUTOR.
    /// </summary>
    [CreateAssetMenu(fileName = "UrchinSpikeAction",
        menuName = "ScriptableObjects/Vessel Actions/Urchin Spike")]
    public class UrchinSpikeActionSO : ShipActionSO
    {
        [Header("Pattern")]
        [Tooltip("The pattern the PRESS fires. ConcentricRings = the aimed shotgun out of every " +
                 "muzzle. The charged release is always Spherical - that is what 'in all " +
                 "directions' means.")]
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Default;

        [Tooltip("Hold to keep firing, or fire once per press. OFF for the shipped weapon: the " +
                 "press is SEMI-AUTOMATIC and the hold belongs to the charge. Turning this on " +
                 "and the charge off restores the old auto-repeat volley.")]
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

        [Header("Charge (hold the trigger, release the burst)")]
        [Tooltip("Holding the trigger charges an omni burst that fires on release. The press " +
                 "itself still fires the aimed pattern, so a tap is an ordinary semi-auto shot " +
                 "and nothing is lost by holding.")]
        [SerializeField] bool chargeEnabled = true;

        [Tooltip("Hold shorter than this and the release fires nothing - the pull was a tap. " +
                 "Keep it above a human's fastest deliberate tap or every shot ends in a burst.")]
        [SerializeField, Range(0.05f, 2f)] float minChargeSeconds = 0.35f;

        [Tooltip("Hold this long for a FULL charge. Holding longer adds nothing, so this is " +
                 "also the weapon's slowest honest cadence.")]
        [SerializeField, Range(0.2f, 10f)] float maxChargeSeconds = 2.5f;

        [Tooltip("Spikes in the omni burst at the MINIMUM charge - what a barely-held trigger " +
                 "throws.")]
        [SerializeField, Range(1, 64)] int chargedSpikesAtMin = 6;

        [Tooltip("Spikes in the omni burst at a FULL charge. 36 is the gapless golden-spiral " +
                 "sphere the old free barrage fired.")]
        [SerializeField, Range(1, 64)] int chargedSpikesAtMax = 36;

        [Tooltip("Ammo the charged burst costs. Authored FREE - the hold is its price, exactly " +
                 "as the barrage it replaces was free and paid in spread.")]
        [SerializeField] float chargedAmmoCost = 0f;

        [Tooltip("Muzzle speed of the charged burst's spikes, before the CHARGE reach " +
                 "multiplier. Slower than the aimed blast: an omni burst is a net, not a shot.")]
        [SerializeField] float chargedProjectileSpeed = 40f;

        [Header("Cost")]
        [SerializeField] int ammoIndex = 0;

        [Tooltip("Ammo per aimed blast (the tap). The charged burst has its own cost and is " +
                 "authored free.")]
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
                 "visibly runs out of steam. The CHARGE level-5 upgrade ('Overcharge') " +
                 "overrides this to 1.")]
        [SerializeField, Range(0.05f, 1f)] float generationRangeFalloff = 0.75f;

        [Tooltip("CHARGE level-5 'Overcharge': grant this ability ONE EXTRA generation. Not a " +
                 "floor-at-one - the upgrade unlocks at Charge 5, where the depth curve already " +
                 "returns at least 1 for every authored pair, so a floor could never bind and " +
                 "the upgrade did nothing. It rides alongside the reach half of the same " +
                 "upgrade (see ResolveRangeFalloff): overcharged, the cascade runs one " +
                 "generation deeper AND stops losing reach as it spreads.")]
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

        public bool ChargeEnabled => chargeEnabled;
        public float MinChargeSeconds => Mathf.Max(0f, minChargeSeconds);
        public float MaxChargeSeconds => Mathf.Max(MinChargeSeconds + 0.01f, maxChargeSeconds);
        public float ChargedAmmoCost => Mathf.Max(0f, chargedAmmoCost);
        public float ChargedProjectileSpeed => chargedProjectileSpeed;

        /// <summary>
        /// How charged a hold of <paramref name="heldSeconds"/> is, 0 at the minimum and 1 at a
        /// full charge. A hold below the minimum is not a charge at all — the executor checks
        /// that first — so 0 here means "the shortest hold that fires anything", not "nothing".
        /// </summary>
        public float Charge01(float heldSeconds)
            => Mathf.Clamp01((heldSeconds - MinChargeSeconds) / (MaxChargeSeconds - MinChargeSeconds));

        /// <summary>
        /// Spikes the release throws for a given charge. Pure and edit-mode testable, and the
        /// ONE place the hold-to-spikes curve lives so the executor cannot grow a second copy.
        /// Clamped to the same 64 the authored fields are ranged to — the golden spiral is
        /// gapless well below that and the pool tiers are sized against it.
        /// </summary>
        public int ChargedSpikeCount(float charge01)
            => Mathf.Clamp(
                   Mathf.RoundToInt(Mathf.Lerp(chargedSpikesAtMin, chargedSpikesAtMax,
                                               Mathf.Clamp01(charge01))),
                   1, 64);

        /// <summary>
        /// CHARGE -> reach. The authored muzzle speed scaled by the vessel's live Charge
        /// multiplier from its <c>ElementalAbilityMapSO</c>. Read per volley at fire time,
        /// never cached across a hold and never bound as an <c>ElementalFloat</c> on this
        /// shared asset (per-vessel state on a shared SO is last-initializer-wins).
        ///
        /// This read was SPACE while the spikes were two abilities and SPACE owned the aimed
        /// one. SPACE now owns the Track Projector on the other trigger, so the whole weapon —
        /// reach and depth alike — belongs to CHARGE, and the map's Charge entry carries the
        /// multiplier the Space entry used to (2.5 at level 10, floored at 0.4).
        /// </summary>
        public float ResolveRangeScale(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            return abilities ? abilities.Multiplier(Element.Charge) : 1f;
        }

        /// <summary>
        /// Per-generation reach decay, or 1 when the pilot holds the CHARGE level-5 upgrade
        /// ("Overcharge") and the wavefront keeps its full reach to the last generation.
        ///
        /// Merging the two spike abilities merged their two upgrades: the old SPACE-5 "Deep
        /// Cascade" (no falloff) and CHARGE-5 "Overcharge" (one extra generation) are now the
        /// two halves of ONE upgrade on the one element the weapon belongs to. Nothing was
        /// dropped — an element may only carry one level-5, and the cascade running deeper and
        /// keeping its reach is a single idea.
        ///
        /// Gated on <c>IsUpgradeActive</c> — the replicated unlock bit — and NOT on a raw local
        /// level read, because this changes the prismscape and a local read desyncs it.
        /// </summary>
        public float ResolveRangeFalloff(IVesselStatus status)
        {
            var abilities = status?.ElementalAbilityHandler;
            bool overcharged = abilities != null && abilities.IsUpgradeActive(Element.Charge);
            return overcharged ? 1f : generationRangeFalloff;
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
