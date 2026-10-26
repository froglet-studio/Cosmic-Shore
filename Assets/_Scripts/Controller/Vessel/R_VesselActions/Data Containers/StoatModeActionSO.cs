using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's MODE switch (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1a, §1f) — the B button (<c>Button2Action</c>;
    /// T on a keyboard). Each press steps the hull to its next mode, Mass → Time → Hybrid → Mass, and the triggers
    /// mean something different in each:
    /// <list type="bullet">
    /// <item><b>Mass mode</b> (the default) — the triggers lay the FIELD DIPOLE, and the trail is laid THICK:
    /// <see cref="MassModeThickness"/> × the prism's own height, MASS's one dial (×3 at rest → ×6 at level 10).
    /// Mass level 5 lays every Mass-mode prism SHIELDED.</item>
    /// <item><b>Time mode</b> — the triggers fly the ORBIT SLINGSHOT (<see cref="StoatSlingExecutor"/>, the
    /// playtest's auto-orbit): press lays the attractor beside the hull and the hull circles it; release a
    /// quarter-turn round and the slingshot pays its whole kick. The trail is laid at its own thickness.</item>
    /// <item><b>Hybrid mode</b> (2026-10-26) — the triggers fly Time mode's orbit, and the pathfinder's path is drawn
    /// too, <see cref="HybridPathLengthShare"/> of Mass mode's length: the line shows where a release right now would
    /// send you, warms toward lime as the quarter-turn nears, and a well-timed release keeps the pathfinder's boost
    /// on for up to <see cref="HybridCarrySeconds"/> (the line stays lime).</item>
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

        [Header("Hybrid mode — the orbit with a short path")]
        [Tooltip("How long Hybrid mode's predicted path is, as a share of Mass mode's (StoatDipoleConfig pathLength). " +
                 "Short on purpose: it shows the exit of an orbit and the next few seconds, not the whole course.")]
        [SerializeField, Range(0.1f, 1f)] float hybridPathLengthShare = 0.5f;

        [Tooltip("Seconds the pathfinder's boost stays on after a PERFECTLY timed Hybrid-mode release (the line stays " +
                 "lime). Scaled by the release's timing, so a release outside the sweet spot carries nothing and the " +
                 "boost fades as it does in Time mode.")]
        [SerializeField, Range(0f, 10f)] float hybridCarrySeconds = 2.5f;

        [Header("Autopilot")]
        [Tooltip("An autopilot switches to Time mode (the orbit) when its target is at least this many degrees off " +
                 "the nose, and back to Mass mode (the dipole) when it is within half of it. 0 = the AI keeps Mass mode. " +
                 "The AI does not choose Hybrid mode; a hull left in it counts as orbiting.")]
        [SerializeField, Range(0f, 180f)] float autopilotTimeModeDegrees = 70f;

        [Tooltip("Seconds an autopilot holds a mode before it may switch again.")]
        [SerializeField, Range(0.5f, 20f)] float autopilotModeIntervalSeconds = 3f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played on every peer when the hull switches INTO Mass mode. Empty = silent.")]
        [SerializeField] EventReference massModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Time mode. Empty = silent.")]
        [SerializeField] EventReference timeModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Hybrid mode. Empty = silent.")]
        [SerializeField] EventReference hybridModeEvent;

        public float BlendSeconds => Mathf.Max(0.05f, blendSeconds);
        public float AutopilotTimeModeDegrees => Mathf.Clamp(autopilotTimeModeDegrees, 0f, 180f);
        public float AutopilotModeIntervalSeconds => Mathf.Clamp(autopilotModeIntervalSeconds, 0.5f, 20f);
        public EventReference MassModeEvent => massModeEvent;
        public EventReference TimeModeEvent => timeModeEvent;
        public EventReference HybridModeEvent => hybridModeEvent;
        public float HybridPathLengthShare => Mathf.Clamp(hybridPathLengthShare, 0.1f, 1f);
        public float HybridCarrySeconds => Mathf.Max(0f, hybridCarrySeconds);

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
