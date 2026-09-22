using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>Fold</b> — TIME. Hold the trigger and the vessel stops; a ghost of it
    /// appears and you place it, anywhere inside the cell, with the two sticks; let go and you
    /// are there. Design record: <c>R_VesselActions/BUTTERFLY_FOLD.md</c>.
    ///
    /// <para><b>The placement is spherical and it uses the dual-stick mix as it already is.</b>
    /// <c>XDiff</c> — the mix's throttle axis, <c>(right.x − left.x + 2)/4</c> — is already 0 with
    /// both thumbs rolled INWARD, 1 with both rolled OUTWARD and exactly <b>0.5</b> at rest, so it
    /// maps onto the cell's radius with nothing to invert or rescale: thumbs in is the core, thumbs
    /// out is the membrane, hands off is half way. <c>XSum</c>/<c>YSum</c> are the azimuth and
    /// elevation of that radius, and <c>YDiff</c> is left alone to do what it always does — ROLL
    /// the vessel, which rolls the camera with it (the camera reads the root's rotation), which
    /// carries this whole spherical frame around with it. That last one is the part that makes it
    /// quick to fly: you are not solving for two angles in a fixed world frame, you are rolling the
    /// world until the place you want is where your thumbs already are.</para>
    ///
    /// <para><b>Outside a membrane it is a different manoeuvre</b>, because there is no sphere to
    /// place anything in: the hold becomes a REACH — the ghost glides out along the heading, further
    /// the longer you hold, out to <see cref="FreeSpaceRange"/>. Same trigger, same ghost, same
    /// release.</para>
    ///
    /// <para><b>TIME's continuous dial is the RECHARGE</b>, which is the one dial that means
    /// something in both branches (inside a cell the reach is the cell, so reach cannot be it).
    /// <b>Time 5 — "Far Fold"</b> doubles the FREE-SPACE range; inside a membrane it changes
    /// nothing, because the membrane is already the limit and an upgrade that promised more there
    /// would be promising something the geometry cannot give.</para>
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

        [Header("Placement (inside a membrane)")]
        [Tooltip("Azimuth the sticks sweep through at full XSum, in degrees each way. 180 gives " +
                 "the whole circle from one thumb sweep.")]
        [SerializeField, Range(15f, 180f)] float azimuthDegrees = 180f;

        [Tooltip("Elevation the sticks sweep through at full YSum, in degrees each way. 90 " +
                 "reaches both poles.")]
        [SerializeField, Range(15f, 90f)] float elevationDegrees = 90f;

        [Tooltip("Fraction of the membrane radius the ghost may reach at full outward thumbs. " +
                 "Under 1 so a fold can never land you ON the membrane, which is a wall.")]
        [SerializeField, Range(0.1f, 1f)] float maxRadiusFraction = 0.94f;

        [Header("Placement (open space)")]
        [Tooltip("How fast the ghost reaches out along the heading while held, world units per " +
                 "second. The hold IS the distance, which is what makes a long fold feel earned.")]
        [SerializeField, Min(1f)] float freeSpaceReachSpeed = 900f;

        [Tooltip("Furthest the open-space fold can reach at the resting Time level.")]
        [SerializeField, Min(1f)] float freeSpaceRange = 1800f;

        [Tooltip("TIME level-5 'Far Fold': multiplies the OPEN-SPACE range only. Inside a " +
                 "membrane the cell is already the limit, so the upgrade deliberately promises " +
                 "nothing there rather than promising something the geometry cannot give.")]
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
        public float AzimuthDegrees => azimuthDegrees;
        public float ElevationDegrees => elevationDegrees;
        public float MaxRadiusFraction => maxRadiusFraction;
        public float FreeSpaceReachSpeed => freeSpaceReachSpeed;
        public float FreeSpaceRange => freeSpaceRange;
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
        /// The open-space reach for THIS fold, including "Far Fold".
        ///
        /// Gated on <c>IsUpgradeActive</c> — the REPLICATED unlock bit — rather than a raw local
        /// level read, because the fold writes a POSE that every peer adopts: two machines
        /// disagreeing about how far it may reach is a vessel in two places.
        /// </summary>
        public float ResolveFreeSpaceRange(IVesselStatus status)
        {
            float range = FreeSpaceRange;
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
