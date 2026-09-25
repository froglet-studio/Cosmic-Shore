using CosmicShore.Data;
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
