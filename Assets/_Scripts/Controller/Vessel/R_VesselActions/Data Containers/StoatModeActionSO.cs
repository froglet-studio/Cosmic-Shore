using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's MODE switch (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1a, §1f) — the B button (<c>Button2Action</c>;
    /// T on a keyboard). Each press steps the hull to its next mode, Mass → Time → Gate → Mass, and the triggers
    /// mean something different in each:
    /// <list type="bullet">
    /// <item><b>Mass mode</b> (the default) — the triggers lay the FIELD DIPOLE, and the trail is laid THICK:
    /// <see cref="MassModeThickness"/> × the prism's own height, MASS's one dial (×3 at rest → ×6 at level 10).
    /// Mass level 5 lays every Mass-mode prism SHIELDED.</item>
    /// <item><b>Time mode</b> — the triggers fly the ORBIT SLINGSHOT (<see cref="StoatSlingExecutor"/>, the
    /// playtest's auto-orbit): press lays the attractor beside the hull and the hull circles it; release a
    /// quarter-turn round and the slingshot pays its whole kick. The trail is laid at its own thickness.</item>
    /// <item><b>Gate mode</b> (2026-10-27, <c>STOAT_DIPOLE.md</c> §1f) — a WARP GATE (<see cref="StoatGateMath"/>, laid by
    /// <see cref="StoatSlingExecutor"/>): a press opens a black hole ahead of the nose and its white hole further along the
    /// same line, the deeper squeeze stretches it, flying into the black hole jumps the hull out of the white hole and warps
    /// its flight — most for a long gate entered straight down its line. Held it lasts a few seconds; let go it falls
    /// together in under one. One gate at a time. Its numbers are this asset's gate rows, cloned from the Vessel Studio.</item>
    /// </list>
    /// <para>A press with no release semantics (vessel rule 37): <see cref="StopAction"/> is a no-op, so the AI's
    /// Start → Stop pair and a human's release never undo the switch. Shared and stateless: the mode lives on
    /// <see cref="StoatModeExecutor"/>, which also reads this asset directly (rule 20).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatModeAction", menuName = "ScriptableObjects/Vessel Actions/Stoat Mode")]
    public sealed class StoatModeActionSO : ShipActionSO
    {
        [Header("Mass mode — the thick trail")]
        [Tooltip("MASS's one dial: how many times THICKER (the prism's height) a Mass-mode trail is laid — ×3 at rest, " +
                 "×6 at level 10, floored at 1 so the deficit band never thins it. Read at the REPLICATED level, so " +
                 "every peer lays the same trail.")]
        [SerializeField] ElementalFloat massModeThickness = ElementalFloat.Multiplier(3f, 6f, Element.Mass, 1f);

        [Tooltip("Seconds the trail takes to thicken fully on entering Mass mode (and to thin again leaving it).")]
        [SerializeField, Min(0.05f)] float blendSeconds = 0.8f;

        [Tooltip("MASS level 5: every prism laid in Mass mode arrives SHIELDED. Gated on the replicated unlock bit.")]
        [SerializeField] bool massUpgradeShieldsInMassMode = true;

        [Header("Gate mode — the warp gate (the studio's gt* rows)")]
        [Tooltip("u ahead of the nose the black hole opens at the press. Studio: gtAhead.")]
        [SerializeField, Min(1f)] float gateAhead = 90f;

        [Tooltip("u from the black hole to the white hole at the lightest squeeze. The jump skips it. Studio: gtLenMin.")]
        [SerializeField, Min(1f)] float gateLengthMin = 120f;

        [Tooltip("u fully squeezed: a longer gate is a longer jump and a stronger warp after it. Studio: gtLenMax.")]
        [SerializeField, Min(1f)] float gateLengthMax = 600f;

        [Tooltip("The black hole's horizon: the mouth flown into to jump, u. Studio: gtMouth.")]
        [SerializeField, Range(0.5f, 40f)] float gateMouthRadius = 5f;

        [Tooltip("Held, a gate stays open at most this long, then falls together on its own. Studio: gtHoldMax.")]
        [SerializeField, Range(0.5f, 10f)] float gateHoldMaxSeconds = 3f;

        [Tooltip("Let go (or flown through), the gate falls together and annihilates in about this long. Studio: gtCollapse.")]
        [SerializeField, Range(0.2f, 3f)] float gateCollapseSeconds = 0.8f;

        [Tooltip("The full warp needs the entry heading within this of the gate's line; none at this angle. Studio: gtAimCone.")]
        [SerializeField, Range(5f, 90f)] float gateAimConeDegrees = 25f;

        [Tooltip("The warp after a perfect jump, as a share of the loop boost (StoatDipoleConfig boost; Time scales it). " +
                 "Studio: gtWarpShare.")]
        [SerializeField, Range(0f, 3f)] float gateWarpShare = 1.5f;

        [Tooltip("Seconds the warp after a jump holds before it fades. Studio: gtBoostSeconds.")]
        [SerializeField, Range(0f, 8f)] float gateBoostSeconds = 2.5f;

        [Tooltip("Keys and touch (no analog trigger): a held key or button reaches a full squeeze after this long. " +
                 "Studio: gtKeyRamp.")]
        [SerializeField, Range(0.05f, 3f)] float gateKeyRampSeconds = 0.5f;

        [Tooltip("Within this of its black hole, still ahead, the gate draws its owner sideways onto the line into its " +
                 "mouth, u. Studio: gtFunnel.")]
        [SerializeField, Range(0f, 120f)] float gateFunnelRadius = 30f;

        [Tooltip("...closing the sideways miss at this rate, per second. Studio: gtFunnelRate.")]
        [SerializeField, Range(0f, 20f)] float gateFunnelRate = 4f;

        [Header("Autopilot")]
        [Tooltip("An autopilot switches to Time mode (the orbit) when its target is at least this many degrees off " +
                 "the nose, and back to Mass mode (the dipole) when it is within half of it. 0 = the AI keeps Mass mode.")]
        [SerializeField, Range(0f, 180f)] float autopilotTimeModeDegrees = 70f;

        [Tooltip("Seconds an autopilot holds a mode before it may switch again.")]
        [SerializeField, Range(0.5f, 20f)] float autopilotModeIntervalSeconds = 3f;

        [Tooltip("An autopilot switches into Gate mode for a long leg: its target at least this far away and within " +
                 "autopilotGateSwitchConeDegrees of the nose. It leaves Gate under half the distance or past twice the " +
                 "cone. 0 = the AI never uses gates.")]
        [SerializeField, Min(0f)] float autopilotGateDistance = 400f;

        [Tooltip("How far off the nose a long leg may start for the autopilot to switch into Gate mode.")]
        [SerializeField, Range(1f, 180f)] float autopilotGateSwitchConeDegrees = 30f;

        [Tooltip("In Gate mode the autopilot opens a gate only with its target at least this far away. Studio: gtAiMin.")]
        [SerializeField, Min(0f)] float autopilotGateMinDistance = 300f;

        [Tooltip("...and within this of its nose: the gate lies along the nose. Studio: gtAiCone.")]
        [SerializeField, Range(1f, 90f)] float autopilotGateConeDegrees = 12f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played on every peer when the hull switches INTO Mass mode. Empty = silent.")]
        [SerializeField] EventReference massModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Time mode. Empty = silent.")]
        [SerializeField] EventReference timeModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Gate mode. Empty = silent.")]
        [SerializeField] EventReference gateModeEvent;

        public float BlendSeconds => Mathf.Max(0.05f, blendSeconds);
        public float AutopilotTimeModeDegrees => Mathf.Clamp(autopilotTimeModeDegrees, 0f, 180f);
        public float AutopilotModeIntervalSeconds => Mathf.Clamp(autopilotModeIntervalSeconds, 0.5f, 20f);
        public EventReference MassModeEvent => massModeEvent;
        public EventReference TimeModeEvent => timeModeEvent;
        public EventReference GateModeEvent => gateModeEvent;
        public float AutopilotGateDistance => Mathf.Max(0f, autopilotGateDistance);
        public float AutopilotGateSwitchConeDegrees => Mathf.Clamp(autopilotGateSwitchConeDegrees, 1f, 180f);
        public float AutopilotGateMinDistance => Mathf.Max(0f, autopilotGateMinDistance);
        public float AutopilotGateConeDegrees => Mathf.Clamp(autopilotGateConeDegrees, 1f, 90f);

        /// <summary>Squeeze ramp for a device without analog triggers in Gate mode (keys, touch).</summary>
        public float GateKeyRampSeconds => Mathf.Clamp(gateKeyRampSeconds, 0.05f, 3f);

        /// <summary>The gate's numbers (<see cref="StoatGateMath"/>).</summary>
        public StoatGateMath.Settings Gate() => new()
        {
            Ahead = Mathf.Max(1f, gateAhead), LengthMin = Mathf.Max(1f, gateLengthMin), LengthMax = Mathf.Max(1f, gateLengthMax),
            Mouth = Mathf.Clamp(gateMouthRadius, 0.5f, 40f), HoldMaxSeconds = Mathf.Clamp(gateHoldMaxSeconds, 0.5f, 10f),
            CollapseSeconds = Mathf.Clamp(gateCollapseSeconds, 0.2f, 3f), AimConeDegrees = Mathf.Clamp(gateAimConeDegrees, 5f, 90f),
            WarpShare = Mathf.Max(0f, gateWarpShare), BoostSeconds = Mathf.Max(0f, gateBoostSeconds),
            FunnelRadius = Mathf.Max(0f, gateFunnelRadius), FunnelRate = Mathf.Max(0f, gateFunnelRate),
        };

        /// <summary>The Mass-mode thickness at this vessel's REPLICATED Mass level (every peer lays the same trail).</summary>
        public float MassModeThickness(IVesselStatus status) => Mathf.Max(1f, massModeThickness.EvaluateReplicated(status));

        /// <summary>Whether Mass-mode prisms arrive shielded right now (the replicated Mass-5 bit, never a local level).</summary>
        public bool ShieldsInMassMode(IVesselStatus status)
        {
            if (!massUpgradeShieldsInMassMode) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Mass);
        }

        // Implicit-bool, never ?. — a destroyed executor is a Unity null that ?. would call into.
        public override void StartAction(ActionExecutorRegistry executors, IVesselStatus status)
        {
            var exec = executors ? executors.Get<StoatModeExecutor>() : null;
            if (exec) exec.Toggle();
        }

        /// <summary>No release semantics: a switch is a press (vessel rule 37).</summary>
        public override void StopAction(ActionExecutorRegistry executors, IVesselStatus status) { }
    }
}
