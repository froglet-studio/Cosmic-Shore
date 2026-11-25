using System.Collections;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Sparrow's barrel roll: a VESSEL DYNAMIC on the RIGHT stick (Shombith, 2026-11-24, moved off
    /// the boost button). No element and no level-5 upgrade touch it - it is how the Sparrow flies, so the
    /// Time scaling it briefly carried (rollTimeScale, 2026-11-18) is gone and the Fleet Codex lists it as
    /// a vessel dynamic with no element.
    ///
    /// Push the right stick past <see cref="engageThreshold"/> in ANY direction and the Sparrow rolls that
    /// way: the stick's direction is the sideways nudge, its horizontal half picks the spin (right half
    /// clockwise, left half counterclockwise). HOLD the stick and it keeps rolling: each roll that ends with
    /// the stick still pushed starts the next one at once, in the stick's direction at that moment. The
    /// Sparrow steers on the LEFT stick only (SingleStickVesselTransformer), so the right stick collides with
    /// nothing - the Scarab's juke (ScarabJukeController) uses its own right stick the same way.
    /// Keyboard: P ; L ' (the right stick of every keyboard strategy, the one-thumb mouse strategy included).
    /// Phone: the touch strategy publishes a right stick for a one-stick hull only from a held AIM button.
    ///
    /// It works IDENTICALLY in the stationary/turret stance. The stance stops travel —
    /// VesselTransformer skips throttle and course travel while IsTranslationRestricted — but
    /// the roll still strafes: its ModifyVelocity displacement
    /// opts out of the restriction (ShipVelocityModifier.ignoresTranslationRestriction), making
    /// it the stopped Sparrow's dodge. The stance itself is unchanged by rolling.
    /// Clockwise on the right half of the stick circle, counterclockwise on the left —
    /// animating the model about its
    /// forward axis while a ModifyVelocity displacement orthogonal to travel (direction picked
    /// by the right stick) carries the vessel sideways. The trail becomes disjointed from
    /// facing, so for the roll duration blockRotation is overridden with the ACTUAL travel
    /// direction (Course + velocityShift): the spawn loop lays travel-aligned bridging prisms,
    /// and remote peers get the same orientation through the owner-written n_BlockRotation.
    ///
    /// The 360° spin lives on the visual child only — the camera reads the root's
    /// rotation/up, Course stays untouched, and a 360° root roll cannot be expressed through
    /// the accumulatedRotation slerp target. A very small REAL root roll (rootRollDegrees,
    /// same handedness as the animation) IS applied through VesselTransformer.ApplyRotation:
    /// it rotates the vessel's UP about its own forward/z axis, which is exactly what the
    /// two-stick Roll() does with a little YDiff, and — because the camera reads the root's
    /// up — it is the horizon tilt the pilot actually feels during the spin. It follows the
    /// animation's own smoothstep, so the tilt eases in and out WITH the spin instead of
    /// drifting linearly across it, and the authored degrees land exactly.
    ///
    /// For the roll's duration the ability OWNS THE ROLL AXIS
    /// (VesselTransformer.BankIntoTurnSuppressed). That is not decoration: the bank-into-turn
    /// and this roll are the same rotation about the same axis, so they add. When the left stick
    /// fired the roll, the trigger WAS peak bank pointing the other way: 15° of authored roll
    /// landed under ~20-25° of opposing bank and the pilot saw the horizon tilt the WRONG way. On
    /// the right stick a hard left-stick turn while rolling still banks, so the suppression stays.
    /// Pitch and yaw are untouched, so the vessel still turns exactly as hard while it rolls.
    ///
    /// Owner-driven: the right stick is a NetworkVariable every peer can read
    /// (InputStatus.n_rNorm), so the trigger is gated on IPlayer.IsLocalPilot exactly as the
    /// Scarab's juke is; the displacement replicates via the owner-authoritative NetworkTransform. Autopilot/AI
    /// vessels never produce stick input, so the roll is inert for AI (trigger synthesis
    /// is tracked in Docs/ElementalAbilitySystem/BACKLOG.md Phase 2.5).
    /// </summary>
    public class BarrelRollController : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Right-stick radial magnitude that starts a roll, in any direction. Uses the " +
                 "normalized (radially clamped) stick vector, never the eased one - the per-axis " +
                 "ease makes diagonal magnitudes direction-dependent. Sits well above the input " +
                 "strategies' own deadzone so a resting stick never rolls.")]
        [SerializeField, Range(0.1f, 1f)] float engageThreshold = 0.6f;
        [Tooltip("Flip the CW/CCW mapping if the roll direction reads backwards in playtest.")]
        [SerializeField] bool invertRollDirection;

        [Header("Roll")]
        [SerializeField, Min(0.1f)] float rollDurationSeconds = 0.6f;
        [Tooltip("Very small REAL roll applied to the vessel ROOT over the roll duration — the " +
                 "vessel's up vector rotated about its own forward/z axis, the same thing a " +
                 "two-stick vessel does with a little YDiff, in the same direction as the visual " +
                 "animation. The camera reads the root's up, so this IS the horizon tilt the " +
                 "pilot feels. Routed through VesselTransformer.ApplyRotation " +
                 "(accumulatedRotation), so the flight orientation keeps the new bank after the " +
                 "roll, and shaped by the animation's own smoothstep so it eases in and out with " +
                 "the spin. The transformer's bank-into-turn is suspended for the duration, so " +
                 "this number is the whole roll the vessel gets rather than a correction on top " +
                 "of a larger opposing bank. 0 = visual-only.")]
        [SerializeField, Range(0f, 45f)] float rootRollDegrees = 15f;
        [Tooltip("Peak sideways displacement speed injected through ModifyVelocity (world " +
                 "units/second; the transformer clamps its channel at 100).")]
        [SerializeField, Min(0f)] float nudgeSpeed = 60f;
        float _rollSeconds;
        [Tooltip("The transform that visually rolls. Defaults to the model's Animator " +
                 "transform, then the vessel root's first child.")]
        [SerializeField] Transform rollVisualTarget;

        // Interface-typed: AutoPilotEnabled and InputStatus are default interface members on
        // IVesselStatus (routed through AIPilot/Player) and are not visible on the concrete
        // VesselStatus type.
        IVesselStatus _status;
        bool _rolling;
        Quaternion _visualRestRotation;

        /// <summary>True while a roll is playing.</summary>
        public bool IsRolling => _rolling;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
        }

        void Update()
        {
            if (_status == null) return;

            // A roll in progress finishes before the next one; holding the stick means the next
            // one starts on the first frame after this one ends (hold to keep rolling).
            if (_rolling) return;
            if (_status.AutoPilotEnabled) return;

            // THE STICK IS REPLICATED, SO THIS MUST BE OWNER-GATED - the same predicate
            // InputController uses to decide who consumes the stick (ScarabJukeController's note).
            if (_status.Player is not { IsLocalPilot: true }) return;

            // NOTE: deliberately NOT gated on IsTranslationRestricted. Stopped in the turret
            // stance the roll is still a roll - the stationary Sparrow's only dodge - so the
            // displacement is exempted from the restriction rather than dropped; see
            // ShipVelocityModifier.ignoresTranslationRestriction.
            var input = _status.InputStatus;
            if (input == null) return;

            // RIGHT stick - the Sparrow steers on the left stick only.
            var stick = input.RightNormalizedJoystickPosition;
            if (stick.magnitude < engageThreshold) return;

            // Right half of the circle -> clockwise (positive angle about +forward is CW
            // from the pilot's seat in Unity's left-handed space); left half -> CCW. Straight
            // up or down rolls clockwise.
            float rollSign = (stick.x >= 0f ? 1f : -1f) * (invertRollDirection ? -1f : 1f);

            var transformer = _status.VesselTransformer;
            if (!transformer) return;

            // The stick's direction picks the orthogonal nudge direction (stick up =
            // nudge up), projected onto the plane orthogonal to travel so the
            // displacement never adds forward/backward speed.
            //
            // Stopped, Course is STALE — MoveShip is what refreshes it and it does not run
            // while restricted, so it holds the heading from the moment the stance engaged
            // while the turret has gone on rotating freely. Project on current facing there.
            bool restricted = _status.IsTranslationRestricted;
            var ship = _status.ShipTransform ? _status.ShipTransform : transform;
            Vector3 nudge = ship.right * stick.x + ship.up * stick.y;
            nudge = Vector3.ProjectOnPlane(nudge, restricted ? transform.forward : _status.Course);
            if (nudge.sqrMagnitude < 1e-4f)
                nudge = ship.right * rollSign;

            // Channelled, not unconditional: a finished ability must not log once per input.
            // IsVerbose first because the message interpolates (a [Conditional] method still
            // evaluates its arguments in the Editor).
            if (CSDebug.IsVerbose(CSLogChannel.SparrowStrafingRoll))
                CSDebug.LogVerbose(CSLogChannel.SparrowStrafingRoll,
                    $"[BarrelRoll] Triggered: {(rollSign > 0f ? "CW" : "CCW")}, " +
                    $"stick ({stick.x:F2}, {stick.y:F2}), nudge dir {nudge.normalized}" +
                    (restricted ? " (stopped — dodge)" : string.Empty));

            _rollSeconds = rollDurationSeconds;
            transformer.ModifyVelocity(nudge.normalized * nudgeSpeed, _rollSeconds,
                                       ignoresTranslationRestriction: true);
            StartCoroutine(RollRoutine(rollSign, transformer));
        }

        IEnumerator RollRoutine(float rollSign, VesselTransformer transformer)
        {
            _rolling = true;

            var visual = ResolveVisualTarget();
            var visualStart = visual ? visual.localRotation : Quaternion.identity;
            _visualRestRotation = visualStart;

            // Roll about the VESSEL's flight forward as seen from the visual target's
            // local frame — the model's authored axes needn't align with flight forward
            // (a raw Vector3.forward spin can read as a wrong-axis wobble or nothing).
            Vector3 localRollAxis = visual
                ? visual.InverseTransformDirection(transform.forward)
                : Vector3.forward;
            if (localRollAxis.sqrMagnitude < 1e-6f)
                localRollAxis = Vector3.forward;

            float elapsed = 0f;
            float rootRollProgress = 0f;

            // The ability owns the roll axis for the duration: the transformer's bank-into-turn
            // is the same rotation about the same axis and would otherwise swamp (and reverse)
            // the root roll below, because the trigger is a full stick deflection — peak bank.
            // Pitch and yaw are untouched. Cleared in the tail AND in OnDisable.
            transformer.BankIntoTurnSuppressed = true;

            while (elapsed < _rollSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _rollSeconds);
                float eased = t * t * (3f - 2f * t);                 // smoothstep 0→1
                float angle = rollSign * 360f * eased;

                if (visual)
                    visual.localRotation = visualStart * Quaternion.AngleAxis(angle, localRollAxis);

                // Very small REAL roll on the vessel ROOT, same handedness as the visual spin:
                // the up vector rotated about the vessel's own forward, i.e. what the two-stick
                // Roll() does with a little YDiff. Routed through accumulatedRotation (which a
                // small angle CAN express, unlike the 360°), so the flight orientation keeps the
                // tilt after the roll — and the camera reads the root's up, so this is the
                // horizon tilt the pilot feels.
                //
                // Advanced by the DELTA of the same smoothstep the animation uses, so the tilt
                // accelerates and settles WITH the spin rather than drifting across it at a
                // constant rate, and the authored degrees land exactly (summing dt/duration
                // overshoots on the frame that ends the loop).
                if (rootRollDegrees > 0f)
                {
                    transformer.ApplyRotation(
                        rollSign * rootRollDegrees * (eased - rootRollProgress),
                        transform.forward);
                    rootRollProgress = eased;
                }

                // Bridging prisms: orient along the actual travel direction each frame while
                // the displacement is live. Skipped while stopped — the stance has already
                // stopped the spawner, so there is no trail to bridge, and Speed/Course are
                // both stale there (see the projection note above).
                if (_status.IsTranslationRestricted)
                {
                    transformer.BlockRotationOverride = null;
                }
                else
                {
                    var travel = _status.Speed * _status.Course + transformer.VelocityShift;
                    transformer.BlockRotationOverride = travel.sqrMagnitude > 1e-4f
                        ? Quaternion.LookRotation(travel.normalized, transform.up)
                        : null;
                }

                yield return null;
            }

            if (visual) visual.localRotation = visualStart;
            transformer.BlockRotationOverride = null;
            transformer.BankIntoTurnSuppressed = false;
            _rolling = false;
        }

        Transform ResolveVisualTarget()
        {
            if (rollVisualTarget) return rollVisualTarget;
            var animator = GetComponentInChildren<Animator>();
            if (animator) return rollVisualTarget = animator.transform;
            return rollVisualTarget = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        void OnDisable()
        {
            // Never leave a half-applied roll behind (pooling / vessel swap safety).
            StopAllCoroutines();
            if (_status?.VesselTransformer)
            {
                _status.VesselTransformer.BlockRotationOverride = null;
                _status.VesselTransformer.BankIntoTurnSuppressed = false;
            }
            if (rollVisualTarget && _rolling)
                rollVisualTarget.localRotation = _visualRestRotation;
            _rolling = false;
        }
    }
}
