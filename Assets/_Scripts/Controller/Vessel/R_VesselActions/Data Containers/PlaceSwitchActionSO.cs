using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's Bulwark-successor: places a SWITCH — a ring of prisms — ahead of the vessel
    /// on its flight COURSE (not its nose: mid-drift you throw the switch where you are going).
    /// Design: R_VesselActions/SCARAB.md §5. Costs one switch charge (a normalized
    /// ResourceSystem meter fraction, refilled by crystals — the Sparrow missile idiom).
    ///
    /// v1 scope: the ring BODY (domain-coloured prisms, bloomed in on the clock, occupancy-
    /// claimed). The ball-deflecting analytic reflector and the mouth-crossing energy trigger
    /// are Astro League mode work (the ball never bounces off prisms — SCARAB.md §5's crux) and
    /// land with the multi-ball pass.
    ///
    /// Element scaling (SCARAB.md §7):
    /// - MASS → structure size (`switchScale` ElementalFloat, ×1 → ×2.5 on the ring radius; the
    ///   map's generic Mass multiplier stays pinned to 1 so the two can't double-dip). Read live
    ///   at use time, never cached (per-use snapshot). MASS 5 additionally builds the switch from
    ///   SHIELDED prisms — see <see cref="ScarabSwitch"/>.
    /// - SPACE does NOT scale placement distance. It owns the forged BALL's size instead
    ///   (×1 → ×4 at Space 10), so `placementDistance` ships with its ElementalFloat DISABLED and
    ///   is a flat authored number. One parameter per element is the contract; leaving both live
    ///   would put two unrelated meanings on one flower.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaceSwitchAction", menuName = "ScriptableObjects/Vessel Actions/Place Switch")]
    public class PlaceSwitchActionSO : ShipActionSO
    {
        [Header("Resource")]
        [Tooltip("Which resource pool holds switch charges.")]
        [SerializeField] int resourceIndex = 1;

        [Tooltip("Charges per full meter. Cost = MaxAmount / this. The executor gates with a " +
                 "small epsilon UNDER the cost: the meter clamps at exactly 1.0 and " +
                 "1.0f − 1/3f − 1/3f lands a float ulp below 1/3f, so an exact-cost gate lets " +
                 "a full meter place only two of three switches (SCARAB.md §3.3's trap).")]
        [SerializeField] float chargesPerFullMeter = 3f;

        [Header("Placement")]
        [Tooltip("How far ahead of the vessel, along its COURSE, the switch ring appears. FLAT: " +
                 "the ElementalFloat is authored disabled because SPACE now owns the forged " +
                 "ball's size. Do not re-enable it without moving ball size off Space.")]
        public ElementalFloat placementDistance = new(150f);

        [Tooltip("Ring radius at scale 1 (world units, centre to brick centres).")]
        [SerializeField, Min(1f)] float ringRadius = 20f;

        [Tooltip("Structure size multiplier. MASS element: enable with Min 1 / Max 2.5.")]
        public ElementalFloat switchScale = new(1f);

        [Header("Ring body")]
        [Tooltip("Prisms filling the DISC inside the ring at placement (a Vogel spiral, so the " +
                 "area fills evenly).")]
        [SerializeField, Range(4, 96)] int interiorPrismCount = 28;

        [Tooltip("Prisms laid when a ball threads the switch — the same spiral CONTINUED outward " +
                 "past the ring's edge, so the payout is the pattern growing rather than a second " +
                 "effect.")]
        [SerializeField, Range(0, 160)] int burstPrismCount = 44;

        [Tooltip("How far past the ring radius the burst reaches, as a multiple of it.")]
        [SerializeField, Min(1f)] float burstRadiusMultiplier = 2.2f;
        [Tooltip("Per-brick target scale. z runs along the ring tangent (prism forward), " +
                 "y points radially (thin), x is the ring's depth along the course axis.")]
        [SerializeField] Vector3 brickScale = new(2.5f, 1.5f, 8f);
        [Tooltip("Grow-clock rate for the bloom-in (the one growth engine — " +
                 "Docs/PRISM_ANIMATION.md).")]
        [SerializeField, Min(0.01f)] float growthRate = 1f;

        public int ResourceIndex => resourceIndex;
        public int InteriorPrismCount => interiorPrismCount;
        public int BurstPrismCount => burstPrismCount;
        public float BurstRadiusMultiplier => burstRadiusMultiplier;
        public Vector3 BrickScale => brickScale;
        public float GrowthRate => growthRate;
        public float RingRadius => ringRadius;

        public float ComputeCost(ResourceSystem rs)
        {
            if (rs == null || resourceIndex < 0 || resourceIndex >= rs.Resources.Count) return 0f;
            var res = rs.Resources[resourceIndex];
            if (res == null || res.MaxAmount <= 0f) return 0f;
            return res.MaxAmount / Mathf.Max(0.0001f, chargesPerFullMeter);
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<PlaceSwitchActionExecutor>()?.PlaceSwitch(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // Placement is a one-shot on press; nothing to stop.
        }
    }
}
