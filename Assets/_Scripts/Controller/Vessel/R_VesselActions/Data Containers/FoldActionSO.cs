using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Fold</b> — TIME. Hold the trigger and the vessel stops; a ghost of it
    /// glides out along the heading, further the longer you hold; let go and you are there.
    /// Design record: <c>R_VesselActions/BUTTERFLY_FOLD.md</c>.
    ///
    /// <para><b>The hold IS the distance, and the heading is the one you arrived on.</b> Pitch and
    /// yaw are dead for the duration (the stop is <c>IsTranslationRestricted</c> and the Butterfly
    /// authors <c>restrictedTurnMultiplier = 0</c>), so a fold cannot be aimed once it has begun —
    /// it is committed to the line the pilot was already flying. On the fleet's slowest hull that
    /// is the whole decision: the fold gives you distance for free, and what it costs is having to
    /// EARN the heading before you press.</para>
    ///
    /// <para><b>There was a second, spherical branch and it is RETIRED.</b> Inside a membrane the
    /// hold used to address a point in the cell's sphere — <c>XDiff</c> the radius, <c>XSum</c> and
    /// <c>YSum</c> the azimuth and elevation, rolled with the vessel — so the ability behaved one
    /// way in a cell and a different way outside one, and the Time-5 upgrade was a NO-OP in the
    /// branch a player spends nearly all of their time in (a membrane is already the limit, so
    /// "further" had nothing to give there). One behaviour everywhere is worth more than two: the
    /// reach is the reach, the upgrade always means something, and there is no boundary a pilot can
    /// cross and find their ability has changed under them.</para>
    ///
    /// <para><b>TIME's continuous dial is the RECHARGE</b>, and <b>Time 5 — "Far Fold"</b> doubles
    /// the reach. With one branch that upgrade is now live everywhere rather than only outside a
    /// cell.</para>
    ///
    /// <para>The asset is SHARED by every Butterfly in a match and holds no per-vessel state —
    /// every number here is read through <see cref="FoldActionExecutor"/>, which owns the hold.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FoldAction",
        menuName = "ScriptableObjects/Vessel Actions/Butterfly Fold")]
    public class FoldActionSO : ShipActionSO
    {
        [Header("Recharge")]
        [Tooltip("Seconds before the Fold can be used again, at the resting Time level. Long on " +
                 "purpose: the Fold crosses a whole cell, so it is a decision rather than a " +
                 "movement option, and a short one would make the vessel's slow cruise pointless.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 30f;

        [Tooltip("TIME -> recharge: the cooldown MULTIPLIER at Time level 10. 0.5 halves the " +
                 "wait. Floored so a deep Time deficit cannot stretch the wait without bound.")]
        [SerializeField] float cooldownMultiplierAtFullTime = 0.5f;

        [Tooltip("Ceiling on the cooldown multiplier, i.e. the worst a Time DEFICIT can make the " +
                 "recharge. Without it the deficit band extrapolates and a debuffed Butterfly " +
                 "loses the ability outright rather than being slowed down.")]
        [SerializeField, Min(1f)] float maxCooldownMultiplier = 2f;

        [Header("Reach")]
        [Tooltip("How fast the ghost reaches out along the heading while held, world units per " +
                 "second. The hold IS the distance, which is what makes a long fold feel earned.")]
        [FormerlySerializedAs("freeSpaceReachSpeed")]
        [SerializeField, Min(1f)] float reachSpeed = 900f;

        [Tooltip("Furthest a fold can reach at the resting Time level.")]
        [FormerlySerializedAs("freeSpaceRange")]
        [SerializeField, Min(1f)] float reachRange = 1800f;

        [Tooltip("TIME level-5 'Far Fold': multiplies the reach. It used to apply OUTSIDE a " +
                 "membrane only, which made it inert wherever a player actually plays; with one " +
                 "reach everywhere it is always worth something.")]
        [SerializeField, Min(1f)] float upgradeRangeMultiplier = 2f;

        [Header("Wormholes (the pair every fold leaves)")]
        [Tooltip("Radius of each of the fold's two wormhole mouths, world units - the sphere is " +
                 "drawn at exactly this and it IS the volume a pilot flies into, so it is " +
                 "simultaneously how big the mouth looks and how precisely you have to fly it.")]
        [SerializeField, Min(1f)] float gateRadius = 55f;

        [Tooltip("Shortest fold that is worth leaving a wormhole for. A tap-and-release puts both " +
                 "ends in the same place, which is a passage to where you already are - so below " +
                 "this NO pair is laid and the previous pair is left standing. One rule covers the " +
                 "degenerate fold and the case where a peer's replicated pose has not landed yet. " +
                 "Never less than 2.5 mouth radii whatever is authored: two mouths must not touch.")]
        [SerializeField, Min(1f)] float minGateSeparation = 300f;

        [Tooltip("Furthest the camera may be from a mouth for it to show the EXACT view through it " +
                 "(the player's camera carried through the pair), world units. Beyond this, plus the " +
                 "fade band, the mouth shows its partner's all-directions panorama instead. The exact " +
                 "view is a second render of the world, so this is also the range that cost is paid " +
                 "over.")]
        [SerializeField, Min(0f)] float portalWindowRange = 2500f;

        [Tooltip("Distance past the exact range over which the exact view crossfades into the " +
                 "panorama, so a mouth never pops between the two.")]
        [SerializeField, Min(1f)] float portalWindowFadeBand = 600f;

        [Tooltip("Resolution of the exact view as a fraction of the gameplay camera's. A mouth can " +
                 "fill the screen as a pilot flies into it, so it is not tiny; it is a second render " +
                 "of the world, so it is not full either. The device tier's ceiling still applies.")]
        [SerializeField, Range(0.25f, 1f)] float portalWindowRenderScale = 0.75f;

        [Tooltip("Texels per side of each of the six panorama faces a mouth's camera captures.")]
        [SerializeField, Range(64, 1024)] int panoramaFaceSize = 256;

        [Tooltip("Surface material for the mouths (CosmicShore/Wormhole). The rim is tinted to the " +
                 "placer's DOMAIN at build; everything else is tuned on the material. Empty = the " +
                 "mouths carry pilots but draw nothing (and say so once).")]
        [SerializeField] Material wormholeMaterial;

        [Tooltip("Seconds a mouth takes to bloom in, and to wither away when the next fold " +
                 "replaces it. Continuity of existence: a portal may not pop into or out of the " +
                 "world any more than a prism may.")]
        [SerializeField, Min(0.01f)] float gateBloomSeconds = 0.45f;

        [Tooltip("Longest a peer waits for the replicated arrival pose before it gives up on " +
                 "laying the destination gate. The owner never waits at all (its pose is local); " +
                 "a peer that is still late at the deadline lays no pair, which is the same " +
                 "outcome as a fold too short to keep.")]
        [SerializeField, Min(0f)] float gateSettleSeconds = 0.75f;

        [Header("Toll (pilots of other domains)")]
        [Tooltip("Whole petals stripped from EACH of a rival's four elements every time they thread " +
                 "this Butterfly's wormhole. Anyone may fly through; a pilot not of the Butterfly's " +
                 "domain pays, and the petals are left on the surface of the mouth they went in by as " +
                 "collectable crystals (one crystal per petal). Clamped to what the pilot holds above " +
                 "level 0, so 15 takes the whole flower, overcharge included. 0 = everyone rides free. " +
                 "The Butterfly itself and its own domain never pay.")]
        [SerializeField, Range(0, 15)] int rivalTollPetalsPerElement = 15;

        [Tooltip("World units/second the stripped crystals are thrown straight out off the mouth's " +
                 "surface. They bleed it off and settle in about two seconds, roughly speed / 1.8 " +
                 "units out (EjectedCrystal) - so this is how far from the surface the spill lands. " +
                 "Floored at 12 so a toll always visibly expels.")]
        [SerializeField, Min(0f)] float rivalTollShedSpeed = 25f;

        [Tooltip("The combat-hit class a toll scores as, for this Butterfly, against the rival who " +
                 "paid it. PvP is petals only and every petal taken is a scored hit (Docs/claude/" +
                 "IMPACT_EFFECTS_AND_AUDIO.md § \"PvP is petals only\"), so a transit that takes the " +
                 "toll scores exactly once, and a pilot warded against WormholeToll neither pays nor " +
                 "is scored on. Debuff: it strips element levels and fires nothing.")]
        [SerializeField] CombatHitClass tollHitClass = CombatHitClass.Debuff;

        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager listens on. Fail-loud: " +
                 "a missing reference throws rather than silently un-scoring the toll.")]
        [SerializeField] ScriptableEventCombatHitStats onTollHitLanded;

        [Header("Feel")]
        [Tooltip("World units per second the ghost eases toward its commanded position. It " +
                 "starts ON the vessel and TRAVELS, so the pilot watches it go rather than " +
                 "finding it already somewhere across the cell.")]
        [SerializeField, Min(1f)] float ghostTravelSpeed = 1400f;

        [Tooltip("Seconds the hull takes to wither away at the origin before the pose is written. " +
                 "Continuity of existence applies to a vessel as much as to a prism — nothing may " +
                 "instantly disappear, and a teleport is a disappearance.")]
        [SerializeField, Min(0f)] float departSeconds = 0.22f;

        [Tooltip("Seconds the hull takes to bloom back in at the destination.")]
        [SerializeField, Min(0f)] float arriveSeconds = 0.3f;

        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public float ReachSpeed => reachSpeed;
        public float ReachRange => reachRange;
        public float GhostTravelSpeed => ghostTravelSpeed;
        public float DepartSeconds => departSeconds;
        public float ArriveSeconds => arriveSeconds;

        public float GateRadius => gateRadius;
        public float MinGateSeparation => Mathf.Max(minGateSeparation, gateRadius * 2.5f);
        public float GateBloomSeconds => gateBloomSeconds;
        public float GateSettleSeconds => gateSettleSeconds;
        public float PortalWindowRange => portalWindowRange;
        public float PortalWindowFadeBand => portalWindowFadeBand;
        public int PanoramaFaceSize => panoramaFaceSize;
        public Material WormholeMaterial => wormholeMaterial;
        public float PortalWindowRenderScale => portalWindowRenderScale;
        public int RivalTollPetalsPerElement => Mathf.Max(0, rivalTollPetalsPerElement);
        public float RivalTollShedSpeed => rivalTollShedSpeed;
        public CombatHitClass TollHitClass => tollHitClass;
        public ScriptableEventCombatHitStats OnTollHitLanded => onTollHitLanded;

        /// <summary>
        /// The recharge this vessel actually pays, at its live TIME level. Read at USE time, never
        /// cached — a crystal collected mid-cooldown should shorten the wait the pilot is serving.
        /// </summary>
        public float ResolveCooldown(IVesselStatus status)
        {
            float multiplier = ElementalScaling.Multiplier(
                status, Element.Time, cooldownMultiplierAtFullTime, cooldownMultiplierAtFullTime);
            return CooldownSeconds * Mathf.Min(multiplier, maxCooldownMultiplier);
        }

        /// <summary>
        /// The reach for THIS fold, including "Far Fold".
        ///
        /// Gated on <c>IsUpgradeActive</c> — the REPLICATED unlock bit — rather than a raw local
        /// level read, because the fold writes a POSE that every peer adopts: two machines
        /// disagreeing about how far it may reach is a vessel in two places.
        /// </summary>
        public float ResolveRange(IVesselStatus status)
        {
            float range = ReachRange;
            var abilities = status?.ElementalAbilityHandler;
            if (abilities != null && abilities.IsUpgradeActive(Element.Time))
                range *= Mathf.Max(1f, upgradeRangeMultiplier);
            return range;
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FoldActionExecutor>()?.Engage(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FoldActionExecutor>()?.Release(this, vesselStatus);
    }
}
