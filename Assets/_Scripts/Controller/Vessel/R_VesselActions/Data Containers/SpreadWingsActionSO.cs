using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's <b>right trigger</b>: it switches the vessel between its two MODES.
    /// Design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.2.
    ///
    /// <list type="bullet">
    /// <item><b>Mass mode</b> (the default, "Spread Wings") — the wings are open and the wake is
    /// WIDE: <see cref="MassModeWidth"/> times the narrow line, where the multiplier is MASS's
    /// continuous dial — <b>5x at Mass 0, 20x at Mass 15</b>, i.e. <c>5 + level</c>. Mass level 5
    /// makes every prism laid in this mode arrive SHIELDED.</item>
    /// <item><b>Dust mode</b> — the wings close, the wake narrows to its 1x line, and the one
    /// skimmer the hull carries (the dust capsule hanging below it, <see cref="ButterflyDustField"/>)
    /// switches on. Everything the dust does lives in that skimmer's effect container.</item>
    /// </list>
    ///
    /// <para><b>There is no energy cost any more</b>, and that is deliberate: a MODE is a choice
    /// between two things the vessel does, not a resource you spend down. What the pilot trades
    /// is the wide wake against the dust, and the trade is the whole decision.</para>
    ///
    /// <para><b>One parameter per element.</b> Mass's number is the WIDTH, so the trail's own
    /// volume ramp (<c>VesselPrismController.trailVolume</c>) is switched off on this hull — two
    /// dials on one element growing the same prism would be the double-dip the fleet convention
    /// exists to prevent.</para>
    ///
    /// <para><b>Every peer lays the SAME wake.</b> The mode rides the replicated press, the
    /// Mass-5 shield the replicated unlock bit, and the width the replicated integer level
    /// (<c>NetElementLevels</c>).</para>
    ///
    /// <para>The asset is SHARED by every Butterfly in a match and holds no per-vessel state.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "SpreadWingsAction",
        menuName = "ScriptableObjects/Vessel Actions/Butterfly Spread Wings")]
    public class SpreadWingsActionSO : ShipActionSO
    {
        public enum ModeInputStyle
        {
            /// <summary>Each press flips the mode. The default.</summary>
            Toggle = 0,
            /// <summary>Dust while the trigger is held, Mass the moment it is released.</summary>
            HoldForDust = 1,
        }

        [Header("Input")]
        [Tooltip("How the trigger switches modes. Toggle: each press flips it. HoldForDust: dust " +
                 "only while the trigger is held.")]
        [SerializeField] ModeInputStyle inputStyle = ModeInputStyle.Toggle;

        [Header("Mass mode — the wide wake")]
        [Tooltip("MASS's continuous dial: how many times WIDER than the dust-mode line the wake is " +
                 "in Mass mode. Evaluated LerpUnclamped over the normalized level, so Min 5 / Max 15 " +
                 "reads 5x at Mass 0, 15x at Mass 10 and 20x at Mass 15 — exactly 5 + level. The " +
                 "floor keeps the deficit band from making Mass mode NARROWER than dust mode.")]
        [SerializeField] ElementalFloat massModeWidth =
            ElementalFloat.Multiplier(5f, 15f, Element.Mass, 1f);

        [Tooltip("Seconds for the wake to open fully from the narrow line (and to close again). " +
                 "The width eases rather than steps, which is what makes a stroke read as a " +
                 "stroke — and it is timed per full swing, so a Mass-15 wake opens in the same " +
                 "time as a Mass-0 one.")]
        [SerializeField, Min(0.05f)] float widthBlendSeconds = 1.5f;

        [Tooltip("MASS level-5: every prism laid in Mass mode arrives SHIELDED. Gated on the " +
                 "replicated unlock bit, per frame, so every peer lays the same tier.")]
        [SerializeField] bool massUpgradeShieldsInMassMode = true;

        public ModeInputStyle InputStyle => inputStyle;
        public float WidthBlendSeconds => Mathf.Max(0.05f, widthBlendSeconds);

        /// <summary>
        /// The Mass-mode width multiplier at this vessel's Mass level — read through
        /// <c>ReplicatedLevel</c>, so every peer that lays this Butterfly's wake lays it at the
        /// SAME width. A local <c>EvaluateLive</c> would read each machine's own copy of the
        /// level, and element levels never replicate. Integer resolution; the executor eases the
        /// width between steps anyway.
        /// </summary>
        public float MassModeWidth(IVesselStatus status) =>
            Mathf.Max(1f, massModeWidth.EvaluateReplicated(status));

        /// <summary>
        /// Whether Mass-mode prisms arrive shielded right now. Gates on <c>IsUpgradeActive</c> —
        /// the replicated bit — never a raw local level read, because what it decides is the tier
        /// of conserved mass every peer lays.
        /// </summary>
        public bool ShieldsInMassMode(IVesselStatus status)
        {
            if (!massUpgradeShieldsInMassMode) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities != null && abilities.IsUpgradeActive(Element.Mass);
        }

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SpreadWingsActionExecutor>()?.Press(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SpreadWingsActionExecutor>()?.Release(this, vesselStatus);
    }
}
