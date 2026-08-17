using System;
using System.Collections;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Sparrow's strafing roll — a BASE part of the boost ability, available from level 0 with no
    /// elemental unlock (it was the TIME level-5 upgrade until the boost redesign; TIME-5 is now the
    /// elemental-debuff immunity held while boosting, see VesselElementalImmunity).
    ///
    /// One roll per BOOST press:
    /// while boosting with the stick at FULL deflection the vessel rolls once — holding the
    /// stick at the perimeter is fine (it never repeats; the next boost press grants the next
    /// roll, and a stick already pinned at max when boost starts rolls immediately). That
    /// once-per-press charge is what the boost ability icon displays
    /// (<see cref="OnRollChargeChanged"/> → SparrowHUDView.SetRollCharge).
    ///
    /// It works IDENTICALLY in the stationary/turret stance. Boost gives no speed there —
    /// VesselTransformer skips throttle and course travel while IsTranslationRestricted — but
    /// the roll is still armed by the press and still strafes: its ModifyVelocity displacement
    /// opts out of the restriction (ShipVelocityModifier.ignoresTranslationRestriction), making
    /// it the stopped Sparrow's dodge. The stance itself is unchanged by rolling.
    /// Clockwise on the right half of the stick circle, counterclockwise on the left —
    /// animating the model about its
    /// forward axis while a ModifyVelocity displacement orthogonal to travel (direction picked
    /// by the left stick) carries the vessel sideways. The trail becomes disjointed from
    /// facing, so for the roll duration blockRotation is overridden with the ACTUAL travel
    /// direction (Course + velocityShift): the spawn loop lays travel-aligned bridging prisms,
    /// and remote peers get the same orientation through the owner-written n_BlockRotation.
    ///
    /// The 360° spin lives on the visual child only — the camera reads the root's
    /// rotation/up, Course stays untouched, and a 360° root roll cannot be expressed through
    /// the accumulatedRotation slerp target. A very small REAL root roll (rootRollDegrees,
    /// same handedness as the animation) IS applied through VesselTransformer.ApplyRotation
    /// so the flight orientation banks subtly with the roll.
    ///
    /// Owner-driven: input polling only acts on the locally controlled vessel; the
    /// displacement replicates via the owner-authoritative NetworkTransform. Autopilot/AI
    /// vessels never produce stick input, so the roll is inert for AI (trigger synthesis
    /// is tracked in Docs/ElementalAbilitySystem/BACKLOG.md Phase 2.5).
    /// </summary>
    public class BarrelRollController : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Stick radial magnitude treated as 'at the perimeter'. 1 = full deflection " +
                 "only (the deadzone processor renormalizes a fully-deflected stick to " +
                 "exactly 1, and the touch joystick clamps at its ring). Uses the " +
                 "normalized (radially clamped) stick vector, never the eased one — the " +
                 "per-axis ease makes diagonal magnitudes direction-dependent.")]
        [SerializeField, Range(0.5f, 1f)] float perimeterThreshold = 1f;
        [Tooltip("Flip the CW/CCW mapping if the roll direction reads backwards in playtest.")]
        [SerializeField] bool invertRollDirection;

        [Header("Roll")]
        [SerializeField, Min(0.1f)] float rollDurationSeconds = 0.6f;
        [Tooltip("Very small REAL roll applied to the vessel root over the roll duration, in " +
                 "the same direction (handedness) as the visual animation. Routed through " +
                 "VesselTransformer.ApplyRotation (accumulatedRotation), so the flight " +
                 "orientation keeps the new bank after the roll. 0 = visual-only.")]
        [SerializeField, Range(0f, 30f)] float rootRollDegrees = 15f;
        [Tooltip("Peak sideways displacement speed injected through ModifyVelocity (world " +
                 "units/second; the transformer clamps its channel at 100).")]
        [SerializeField, Min(0f)] float nudgeSpeed = 60f;
        [Tooltip("The transform that visually rolls. Defaults to the model's Animator " +
                 "transform, then the vessel root's first child.")]
        [SerializeField] Transform rollVisualTarget;

        // Float-safe "at max" comparison: deadzone renormalization / touch clamping land
        // a hair under 1 on some frames.
        const float ThresholdEpsilon = 0.005f;

        // Interface-typed: AutoPilotEnabled and InputStatus are default interface members on
        // IVesselStatus (routed through AIPilot/Player) and are not visible on the concrete
        // VesselStatus type.
        IVesselStatus _status;
        bool _rolling;
        bool _wasBoosting;
        bool _rollArmed;
        Quaternion _visualRestRotation;

        /// <summary>
        /// Raised when the once-per-press roll charge is armed (true, on a fresh boost press) or spent
        /// (false, the instant a roll triggers). The Sparrow HUD binds this to the boost ability icon's
        /// charge ring, which is why the roll is legible without a heat gauge.
        /// </summary>
        public event Action<bool> OnRollChargeChanged;

        /// <summary>True while a strafing roll is available on the current boost press.</summary>
        public bool IsRollArmed => _rollArmed;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
        }

        void Update()
        {
            if (_status == null) return;

            // One roll per BOOST press: a fresh IsBoosting false→true transition arms a
            // roll; triggering consumes it. The stick only needs to BE at the perimeter
            // when the gates pass — a stick already pinned at max when boost starts
            // rolls immediately, and holding it there never repeats (the next boost
            // press grants the next roll).
            bool boosting = _status.IsBoosting;
            if (boosting && !_wasBoosting) SetRollArmed(true);
            _wasBoosting = boosting;

            if (!_rollArmed || _rolling) return;
            if (!boosting) return;
            if (_status.AutoPilotEnabled) return;

            // NOTE: deliberately NOT gated on IsTranslationRestricted. Stopped, the boost
            // gives no speed (VesselTransformer skips throttle/course travel) but the roll is
            // still a roll — one per press, same as flying. It is the stationary Sparrow's only
            // dodge, so the displacement is exempted from the restriction rather than dropped;
            // see ShipVelocityModifier.ignoresTranslationRestriction.
            var input = _status.InputStatus;
            if (input == null) return;

            // LEFT stick only — the steering stick. (The right stick is not a Sparrow
            // input; the earlier right-stick trigger was a spec typo.)
            var stick = input.LeftNormalizedJoystickPosition;
            if (stick.magnitude < perimeterThreshold - ThresholdEpsilon) return;

            // Right half of the circle → clockwise (positive angle about +forward is CW
            // from the pilot's seat in Unity's left-handed space); left half → CCW.
            // invertRollDirection is a taste toggle if the mapping reads backwards in
            // playtest.
            float rollSign = (stick.x >= 0f ? 1f : -1f) * (invertRollDirection ? -1f : 1f);

            var transformer = _status.VesselTransformer;
            if (!transformer) return;

            // The stick's deflection picks the orthogonal nudge direction (stick up =
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

            CSDebug.Log($"[BarrelRoll] Triggered: {(rollSign > 0f ? "CW" : "CCW")}, " +
                        $"stick ({stick.x:F2}, {stick.y:F2}), nudge dir {nudge.normalized}" +
                        (restricted ? " (stopped — dodge)" : string.Empty));

            SetRollArmed(false);
            transformer.ModifyVelocity(nudge.normalized * nudgeSpeed, rollDurationSeconds,
                                       ignoresTranslationRestriction: true);
            StartCoroutine(RollRoutine(rollSign, transformer));
        }

        void SetRollArmed(bool armed)
        {
            if (armed == _rollArmed) return;
            _rollArmed = armed;
            OnRollChargeChanged?.Invoke(armed);
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

            while (elapsed < rollDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rollDurationSeconds);
                float angle = rollSign * 360f * (t * t * (3f - 2f * t)); // smoothstep 0→360

                if (visual)
                    visual.localRotation = visualStart * Quaternion.AngleAxis(angle, localRollAxis);

                // Very small REAL roll on the root, same handedness as the visual spin —
                // distributed linearly across the duration and routed through
                // accumulatedRotation (which a small angle CAN express, unlike the 360°),
                // so the flight orientation banks with the roll and keeps the tilt after.
                if (rootRollDegrees > 0f)
                    transformer.ApplyRotation(
                        rollSign * rootRollDegrees * (Time.deltaTime / rollDurationSeconds),
                        transform.forward);

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
                _status.VesselTransformer.BlockRotationOverride = null;
            if (rollVisualTarget && _rolling)
                rollVisualTarget.localRotation = _visualRestRotation;
            _rolling = false;

            // A re-enabled vessel must not inherit a stale charge — the next boost PRESS arms it,
            // and the HUD re-seeds from IsRollArmed at init.
            _wasBoosting = false;
            SetRollArmed(false);
        }
    }
}
