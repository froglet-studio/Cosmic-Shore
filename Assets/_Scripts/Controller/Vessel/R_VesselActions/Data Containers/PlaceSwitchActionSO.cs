using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's Bulwark-successor: places a SWITCH — an empty ring — ahead of the vessel
    /// on its flight COURSE (not its nose: mid-drift you throw the switch where you are going).
    /// Design: R_VesselActions/SCARAB.md §5. Costs one switch charge (a normalized
    /// ResourceSystem meter fraction, refilled by crystals — the Sparrow missile idiom).
    ///
    /// v1 scope: the ring itself (no interior fill — see the 2026-08-24 "Superseded" note in
    /// <see cref="ScarabSwitch"/>) plus the scarab-wing DAIS it pays out when a ball threads it
    /// (<see cref="ScarabWingDais"/>). The ball-deflecting analytic reflector is Astro League
    /// mode work (the ball never bounces off prisms — SCARAB.md §5's crux) and lands with the
    /// multi-ball pass.
    ///
    /// <para><b>CHARGES RECHARGE ON A COOLDOWN (2026-09-05).</b> They did not, and that was the
    /// whole defect: the Scarab prefab authors "Switch Charges" with
    /// <c>resourceGainRate 0</c> and one charge banked, and the only refill in the game is
    /// <c>ScarabSwitchChargeByCrystalEffect</c>, wired to the four ELEMENTAL crystal branches —
    /// while both Scarab arenas stock OMNI crystals, which the skimmer converts into balls and
    /// which therefore never reach that effect. So a pilot placed exactly one switch per life and
    /// the ability was, in practice, single-use. <see cref="RechargeSecondsPerCharge"/> is the
    /// fix, and it lives HERE rather than on the meter's <c>resourceGainRate</c> so the ability's
    /// cost and its cadence are authored a line apart, and so a mode or a future element could
    /// reach it. The crystal grant is unchanged and still stacks on top — a Scarab that collects
    /// elemental crystals re-arms faster than one that does not.</para>
    ///
    /// <para><b>A pilot holds at most <see cref="MaxLiveSwitches"/> unspent switches.</b> An
    /// unstruck switch lives for the whole match by design (no timer, nothing expires), so a
    /// recharging placer would otherwise litter an arena without bound — and would do it forever
    /// in freestyle, where a match never ends. Placing past the ceiling RETIRES the oldest ring:
    /// active removal caused by a player placing one too many, the same shape as the ball's cell
    /// overload (<c>AstroLeagueBall.DetonateAllLooseInCellServer</c>), never a clock. A retired
    /// ring shrinks away over a visible beat rather than vanishing, and pays no dais — a switch
    /// nobody threaded earned nothing.</para>
    ///
    /// Element scaling (SCARAB.md §7):
    /// - MASS → structure size (`switchScale` ElementalFloat, ×1 → ×2.5 on the ring radius; the
    ///   map's generic Mass multiplier stays pinned to 1 so the two can't double-dip). Read live
    ///   at use time, never cached (per-use snapshot). MASS 5's "Armored Switch" upgrade
    ///   (shielded switch-body prisms) has no fill left to apply to since the interior fill was
    ///   retired — see <see cref="ScarabSwitch"/>.
    /// - SPACE does NOT scale placement distance. It owns the forged BALL's size instead
    ///   (×1 → ×4 at Space 10), so `placementDistance` ships with its ElementalFloat DISABLED and
    ///   is a flat authored number. One parameter per element is the contract; leaving both live
    ///   would put two unrelated meanings on one flower.
    /// - The RECHARGE is deliberately NOT element-scaled. Mass already owns this ability's one
    ///   parameter (aperture) and Charge already owns a cooldown (the cavitation blast), so
    ///   scaling the cadence here would either double-dip Mass or put two meanings on one flower.
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

        [Header("Cooldown")]
        [Tooltip("Seconds to earn ONE switch charge back. The bank therefore refills in " +
                 "this x chargesPerFullMeter seconds. 0 disables the recharge entirely and " +
                 "returns the ability to crystal-only refills, which is what shipped before " +
                 "2026-09-05 and made the switch effectively single-use. Applied smoothly by " +
                 "PlaceSwitchActionExecutor rather than by the meter's own 1 Hz gain coroutine, " +
                 "so the HUD count arrives the frame it is earned.")]
        [SerializeField, Min(0f)] float rechargeSecondsPerCharge = 20f;

        [Tooltip("Charges refunded to the PLACER when a ball threads one of their switches - " +
                 "SCARAB.md 5's \"it pays\", stated in the currency the switch itself spends. At " +
                 "1 a threaded switch is FREE and only a switch nobody used costs you anything, " +
                 "which is what makes placement (rather than placement RATE) the skill. 0 " +
                 "restores the pre-2026-09-05 behaviour, where a threading paid nothing at all.")]
        [SerializeField, Min(0f)] float chargeRefundOnThread = 1f;

        [Tooltip("How many UNSPENT switches one pilot may have standing. Placing past this " +
                 "retires that pilot's oldest ring (it shrinks away and pays no dais). Nothing " +
                 "is ever removed on a timer - the removal is caused by the placement.")]
        [SerializeField, Min(1)] int maxLiveSwitches = 3;

        [Header("Placement")]
        [Tooltip("How far ahead of the vessel, along its COURSE, the switch ring appears. FLAT: " +
                 "the ElementalFloat is authored disabled because SPACE now owns the forged " +
                 "ball's size. Do not re-enable it without moving ball size off Space.")]
        public ElementalFloat placementDistance = new(150f);

        [Tooltip("Ring radius at scale 1 (world units). 2026-08-24: raised 20% (20 -> 24) and " +
                 "the interior Vogel-spiral fill was removed — the ring now blooms in empty, " +
                 "the payout dais is the switch's only prism mass.")]
        [SerializeField, Min(1f)] float ringRadius = 24f;

        [Tooltip("Structure size multiplier. MASS element: enable with Min 1 / Max 2.5.")]
        public ElementalFloat switchScale = new(1f);

        [Header("Ring body")]
        [Tooltip("Grow-clock rate for the dais bloom-in (the one growth engine — " +
                 "Docs/PRISM_ANIMATION.md).")]
        [SerializeField, Min(0.01f)] float growthRate = 1f;

        [Tooltip("Seconds a retired (over-the-ceiling) ring takes to shrink away. Continuity of " +
                 "existence applies to a ring exactly as it does to a prism: nothing vanishes.")]
        [SerializeField, Min(0.05f)] float retireSeconds = 0.5f;

        [Header("Payout — the scarab-wing dais")]
        [Tooltip("Shape of the rosette a threaded switch raises. Every distance is a multiple of " +
                 "the RING RADIUS, so Mass grows the dais with the switch and there is still one " +
                 "size dial. Retuning it moves a SOLVED solution — ScarabWingDaisTests is the gate " +
                 "and will fail rather than let a clipping rosette ship. See ScarabWingDais for " +
                 "the motif and the fitting rules.")]
        [SerializeField] ScarabWingDaisSettings dais = ScarabWingDaisSettings.Default;

        [Tooltip("Prisms laid per frame while the dais blooms outward. The rosette is 255 prisms " +
                 "at the shipped shape, so laying it in one frame would spike; this is a pacing " +
                 "dial, not a cap — every prism is always laid.")]
        [SerializeField, Range(1, 96)] int daisPrismsPerFrame = 24;

        public int ResourceIndex => resourceIndex;
        public float ChargesPerFullMeter => Mathf.Max(0.0001f, chargesPerFullMeter);
        public float RechargeSecondsPerCharge => rechargeSecondsPerCharge;
        public float ChargeRefundOnThread => chargeRefundOnThread;
        public int MaxLiveSwitches => Mathf.Max(1, maxLiveSwitches);
        public float GrowthRate => growthRate;
        public float RetireSeconds => retireSeconds;
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

            // Fields ADDED to an existing asset deserialize as ZERO, and zero means something
            // different for each of these: a zero ceiling would refuse every placement, and a
            // zero retire time would make a ring vanish. The recharge is the one field where
            // zero is a real authored choice (crystal-only refills), so it is left alone.
            if (maxLiveSwitches <= 0) maxLiveSwitches = 3;
            if (retireSeconds < 0.05f) retireSeconds = 0.5f;
            if (chargesPerFullMeter <= 0f) chargesPerFullMeter = 3f;
        }

        public float ComputeCost(ResourceSystem rs)
        {
            if (rs == null || resourceIndex < 0 || resourceIndex >= rs.Resources.Count) return 0f;
            var res = rs.Resources[resourceIndex];
            if (res == null || res.MaxAmount <= 0f) return 0f;
            return res.MaxAmount / ChargesPerFullMeter;
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<PlaceSwitchActionExecutor>()?.PlaceSwitch(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            // Placement is a one-shot on press; nothing to stop.
        }
    }
}
