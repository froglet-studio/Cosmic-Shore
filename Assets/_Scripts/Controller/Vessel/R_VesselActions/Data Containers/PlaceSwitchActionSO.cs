using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's Bulwark-successor: places a SWITCH — a ring of prisms — ahead of the vessel
    /// on its flight COURSE (not its nose: mid-drift you throw the switch where you are going).
    /// Design: R_VesselActions/SCARAB.md §5. Costs one switch charge (a normalized
    /// ResourceSystem meter fraction, refilled by crystals — the Sparrow missile idiom).
    ///
    /// v1 scope: the ring BODY (domain-coloured prisms, bloomed in on the clock) plus the
    /// scarab-wing DAIS it pays out when a ball threads it (<see cref="ScarabWingDais"/>). The
    /// ball-deflecting analytic reflector is Astro League mode work (the ball never bounces off
    /// prisms — SCARAB.md §5's crux) and lands with the multi-ball pass.
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
        [Tooltip("Per-brick target scale. z runs along the ring tangent (prism forward), " +
                 "y points radially (thin), x is the ring's depth along the course axis.")]
        [SerializeField] Vector3 brickScale = new(2.5f, 1.5f, 8f);
        [Tooltip("Grow-clock rate for the bloom-in (the one growth engine — " +
                 "Docs/PRISM_ANIMATION.md).")]
        [SerializeField, Min(0.01f)] float growthRate = 1f;

        [Header("Payout — the scarab-wing dais")]
        [Tooltip("Shape of the rosette a threaded switch raises. Every distance is a multiple of " +
                 "the RING RADIUS, so Mass grows the dais with the switch and there is still one " +
                 "size dial. Author it in FrogletTools > Vessels > Scarab Wing Dais Lab, which " +
                 "draws it and runs the overlap checks; see ScarabWingDais for the motif and the " +
                 "fitting rules.")]
        [SerializeField] ScarabWingDaisSettings dais = ScarabWingDaisSettings.Default;

        [Tooltip("Prisms laid per frame while the dais blooms outward. The rosette is 255 prisms " +
                 "at the shipped shape, so laying it in one frame would spike; this is a pacing " +
                 "dial, not a cap — every prism is always laid.")]
        [SerializeField, Range(1, 96)] int daisPrismsPerFrame = 24;

        public int ResourceIndex => resourceIndex;
        public int InteriorPrismCount => interiorPrismCount;
        public Vector3 BrickScale => brickScale;
        public float GrowthRate => growthRate;
        public float RingRadius => ringRadius;
        public int DaisPrismsPerFrame => daisPrismsPerFrame;

        /// <summary>
        /// The authored dais shape, with a fallback for the asset that predates it.
        /// A struct field ADDED to an existing asset deserializes as all-zero — which here means
        /// a zero-pair, zero-blade rosette, i.e. no payout at all and nothing on screen to say
        /// why. <see cref="ScarabWingDaisSettings.PairCount"/> is range-gated at 3 in the
        /// inspector, so zero can only mean "never authored".
        /// </summary>
        public ScarabWingDaisSettings Dais => dais.PairCount > 0 ? dais : ScarabWingDaisSettings.Default;

        void OnValidate()
        {
            // Repair the pre-dais asset in place so the inspector shows the real motif rather
            // than a field of zeros the first time someone opens it.
            if (dais.PairCount <= 0) dais = ScarabWingDaisSettings.Default;
        }

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
