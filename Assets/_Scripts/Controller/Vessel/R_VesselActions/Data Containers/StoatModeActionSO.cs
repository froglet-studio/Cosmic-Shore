using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's MODE switch (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1a, §1f) — the B button (<c>Button2Action</c>;
    /// T on a keyboard). Each press steps the hull to its next mode, Mass → Time → Drive → Mass, and the triggers
    /// mean something different in each:
    /// <list type="bullet">
    /// <item><b>Mass mode</b> (the default) — the triggers lay the FIELD DIPOLE, and the trail is laid THICK:
    /// <see cref="MassModeThickness"/> × the prism's own height, MASS's one dial (×3 at rest → ×6 at level 10).
    /// Mass level 5 lays every Mass-mode prism SHIELDED.</item>
    /// <item><b>Time mode</b> — the triggers fly the ORBIT SLINGSHOT (<see cref="StoatSlingExecutor"/>, the
    /// playtest's auto-orbit): press lays the attractor beside the hull and the hull circles it; release a
    /// quarter-turn round and the slingshot pays its whole kick. The trail is laid at its own thickness.</item>
    /// <item><b>Drive mode</b> (2026-10-26, <c>STOAT_DIPOLE.md</c> §1f) — the triggers shape a WARP PAIR that rides with
    /// the hull (<see cref="StoatDriveMath"/>): both stretch it for speed (the long legs), the difference tilts it and
    /// the tilt turns you; the sticks turn on the real clock, so a long fast leg turns wide and the pair carves. The
    /// pathfinder flies it and draws its line. Its numbers are this asset's drive rows, cloned from the Vessel Studio.</item>
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

        [Header("Drive mode — the warp pair (the studio's dr* rows)")]
        [Tooltip("u the black hole rides ahead (and the white hole behind) with the triggers barely squeezed. A short " +
                 "pair tilts a long way for a small difference: sharp, agile turns. Studio: drLenMin.")]
        [SerializeField, Min(1f)] float driveLengthMin = 60f;

        [Tooltip("u with both triggers full. A long pair barely tilts, so the fastest travel is also the straightest. " +
                 "Studio: drLenMax.")]
        [SerializeField, Min(1f)] float driveLengthMax = 320f;

        [Tooltip("u the black hole slides to the deeper trigger's side at a full difference (the white hole the same the " +
                 "other way). More reach, more tilt, more turn. Studio: drLatMax.")]
        [SerializeField, Min(0f)] float driveLateralMax = 140f;

        [Tooltip("Degrees per second of turn per degree the pair is tilted, flown on the warped clock, so the arc the line " +
                 "shows is the arc flown at any speed. Studio: drTurnGain.")]
        [SerializeField, Range(0f, 6f)] float driveTurnGain = 2.2f;

        [Tooltip("The fastest the pair turns the hull, deg/s, on top of the sticks. Studio: drTurnMax.")]
        [SerializeField, Range(0f, 360f)] float driveTurnMax = 140f;

        [Tooltip("The warp at full stretch, as a share of the loop boost (StoatDipoleConfig boost; Time scales it). " +
                 "1 = the field's lime warp. Studio: drWarpShare.")]
        [SerializeField, Range(0f, 3f)] float driveWarpShare = 1f;

        [Tooltip("How the warp grows with the stretch: above 1 a light squeeze stays near the hull's own speed (room to " +
                 "trim) and the warp comes on as the squeeze deepens. Studio: drWarpExp.")]
        [SerializeField, Range(0.5f, 3f)] float driveWarpExponent = 1.3f;

        [Tooltip("Keys and touch (no analog trigger): a held key or button reaches a full squeeze after this long. " +
                 "Studio: drKeyRamp.")]
        [SerializeField, Range(0.05f, 3f)] float driveKeyRampSeconds = 0.5f;

        [Tooltip("World radius of the ring drawn where each pole rides (presentation only).")]
        [SerializeField, Min(0.5f)] float driveMarkerRadius = 10f;

        [Tooltip("The black hole's ring, ahead of the nose.")]
        [SerializeField] Color driveSinkColor = new(0.77f, 0.55f, 1f, 0.95f);

        [Tooltip("The white hole's ring, behind the hull.")]
        [SerializeField] Color driveSourceColor = new(1f, 1f, 1f, 0.95f);

        [Header("Autopilot")]
        [Tooltip("An autopilot switches to Time mode (the orbit) when its target is at least this many degrees off " +
                 "the nose, and back to Mass mode (the dipole) when it is within half of it. 0 = the AI keeps Mass mode.")]
        [SerializeField, Range(0f, 180f)] float autopilotTimeModeDegrees = 70f;

        [Tooltip("Seconds an autopilot holds a mode before it may switch again.")]
        [SerializeField, Range(0.5f, 20f)] float autopilotModeIntervalSeconds = 3f;

        [Tooltip("An autopilot switches into Drive mode for a long leg: its target at least this far away and within " +
                 "autopilotDriveSwitchConeDegrees of the nose. It leaves Drive under half the distance or past twice the " +
                 "cone. 0 = the AI never drives.")]
        [SerializeField, Min(0f)] float autopilotDriveDistance = 400f;

        [Tooltip("How far off the nose a long leg may start for the autopilot to switch into Drive mode.")]
        [SerializeField, Range(1f, 180f)] float autopilotDriveSwitchConeDegrees = 30f;

        [Tooltip("In Drive mode the autopilot stops stretching the pair this close to its target. Studio: drAiNear.")]
        [SerializeField, Min(0f)] float autopilotDriveNear = 0f;

        [Tooltip("...and stretches it fully from this far. Studio: drAiFar.")]
        [SerializeField, Min(1f)] float autopilotDriveFar = 150f;

        [Tooltip("...and not at all while its target is this far off the nose: it turns first, then travels. " +
                 "Studio: drAiCone.")]
        [SerializeField, Range(1f, 180f)] float autopilotDriveConeDegrees = 120f;

        [Tooltip("...and squeezes one trigger fully deeper with this much yaw still to make. Studio: drAiLeanDeg.")]
        [SerializeField, Range(1f, 120f)] float autopilotDriveLeanDegrees = 15f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played on every peer when the hull switches INTO Mass mode. Empty = silent.")]
        [SerializeField] EventReference massModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Time mode. Empty = silent.")]
        [SerializeField] EventReference timeModeEvent;

        [Tooltip("Played on every peer when the hull switches INTO Drive mode. Empty = silent.")]
        [SerializeField] EventReference driveModeEvent;

        public float BlendSeconds => Mathf.Max(0.05f, blendSeconds);
        public float AutopilotTimeModeDegrees => Mathf.Clamp(autopilotTimeModeDegrees, 0f, 180f);
        public float AutopilotModeIntervalSeconds => Mathf.Clamp(autopilotModeIntervalSeconds, 0.5f, 20f);
        public EventReference MassModeEvent => massModeEvent;
        public EventReference TimeModeEvent => timeModeEvent;
        public EventReference DriveModeEvent => driveModeEvent;
        public float DriveWarpShare => Mathf.Max(0f, driveWarpShare);
        public float DriveMarkerRadius => Mathf.Max(0.5f, driveMarkerRadius);
        public Color DriveSinkColor => driveSinkColor;
        public Color DriveSourceColor => driveSourceColor;
        public float AutopilotDriveDistance => Mathf.Max(0f, autopilotDriveDistance);
        public float AutopilotDriveSwitchConeDegrees => Mathf.Clamp(autopilotDriveSwitchConeDegrees, 1f, 180f);

        /// <summary>Squeeze ramp for a device without analog triggers in Drive mode (keys, touch).</summary>
        public float DriveKeyRampSeconds => Mathf.Clamp(driveKeyRampSeconds, 0.05f, 3f);

        /// <summary>The warp pair's geometry (<see cref="StoatDriveMath.Evaluate"/>).</summary>
        public StoatDriveMath.Settings Drive() => new()
        {
            LengthMin = Mathf.Max(1f, driveLengthMin), LengthMax = Mathf.Max(1f, driveLengthMax),
            LateralMax = Mathf.Max(0f, driveLateralMax), TurnGain = Mathf.Max(0f, driveTurnGain),
            TurnMax = Mathf.Max(0f, driveTurnMax), WarpExponent = Mathf.Max(0.05f, driveWarpExponent),
        };

        /// <summary>The autopilot's squeezes in Drive mode (<see cref="StoatDriveMath.AutopilotSqueeze"/>).</summary>
        public StoatDriveMath.AutopilotSettings DriveAutopilot() => new()
        {
            Near = Mathf.Max(0f, autopilotDriveNear), Far = Mathf.Max(1f, autopilotDriveFar),
            ConeDegrees = Mathf.Clamp(autopilotDriveConeDegrees, 1f, 180f), LeanDegrees = Mathf.Clamp(autopilotDriveLeanDegrees, 1f, 120f),
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
