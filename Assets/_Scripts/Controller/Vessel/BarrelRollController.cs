using System.Collections;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// TIME level-5 'Barrel Roll' (ElementalAbilityMapSO upgrade). While boosting, pushing the
    /// stick to FULL deflection rolls the vessel once — one roll per press (the stick must
    /// leave the perimeter before another can trigger) — clockwise on the right half
    /// of the stick circle, counterclockwise on the left — animating the model about its
    /// forward axis while a ModifyVelocity displacement orthogonal to travel (direction picked
    /// by the left stick) carries the vessel sideways. The trail becomes disjointed from
    /// facing, so for the roll duration blockRotation is overridden with the ACTUAL travel
    /// direction (Course + velocityShift): the spawn loop lays travel-aligned bridging prisms,
    /// and remote peers get the same orientation through the owner-written n_BlockRotation.
    ///
    /// Only the visual child rolls — never the root transform (the camera reads the root's
    /// rotation/up, Course stays untouched, and a 360° root roll cannot be expressed through
    /// the accumulatedRotation slerp target).
    ///
    /// Owner-driven: input polling only acts on the locally controlled vessel; the
    /// displacement replicates via the owner-authoritative NetworkTransform. Autopilot/AI
    /// vessels never produce stick input, so the upgrade is inert for AI (trigger synthesis
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
        [Tooltip("The stick must fall back below this magnitude before another roll can " +
                 "trigger — one roll per press; holding the stick at the perimeter never " +
                 "repeats. Keep below perimeterThreshold (hysteresis against edge jitter).")]
        [SerializeField, Range(0f, 1f)] float rearmThreshold = 0.9f;
        [Tooltip("Seconds after a roll completes before another can trigger.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 1.2f;
        [Tooltip("Flip the CW/CCW mapping if the roll direction reads backwards in playtest.")]
        [SerializeField] bool invertRollDirection;

        [Header("Roll")]
        [SerializeField, Min(0.1f)] float rollDurationSeconds = 0.6f;
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
        bool _stickHeldAtPerimeter;
        float _nextAllowedTime;
        Quaternion _visualRestRotation;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
        }

        void Update()
        {
            if (_status == null) return;

            var input = _status.InputStatus;

            // LEFT stick only — the steering stick. (The right stick is not a Sparrow
            // input; the earlier right-stick trigger was a spec typo.)
            var stick = input?.LeftNormalizedJoystickPosition ?? Vector2.zero;

            // Edge-trigger with hysteresis: a roll fires only on the frame the stick
            // REACHES the perimeter, and the stick must fall back below rearmThreshold
            // before it counts as a new press — one roll per press, holding never
            // repeats. Stick tracking runs before every other gate so a stick already
            // pinned at max when boosting starts (or the upgrade activates) doesn't
            // trigger a roll retroactively.
            bool pressed = _stickHeldAtPerimeter
                ? stick.magnitude > rearmThreshold
                : stick.magnitude >= perimeterThreshold - ThresholdEpsilon;
            bool risingEdge = pressed && !_stickHeldAtPerimeter;
            _stickHeldAtPerimeter = pressed;

            if (!risingEdge) return;
            if (_rolling || Time.time < _nextAllowedTime) return;
            if (_status.AutoPilotEnabled) return;
            if (_status.IsTranslationRestricted) return;
            if (!_status.IsBoosting) return;
            if (!_status.ElementalAbilityHandler.IsUpgradeActive(Element.Time)) return;

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
            var ship = _status.ShipTransform ? _status.ShipTransform : transform;
            Vector3 nudge = ship.right * stick.x + ship.up * stick.y;
            nudge = Vector3.ProjectOnPlane(nudge, _status.Course);
            if (nudge.sqrMagnitude < 1e-4f)
                nudge = ship.right * rollSign;

            CSDebug.Log($"[BarrelRoll] Triggered: {(rollSign > 0f ? "CW" : "CCW")}, " +
                        $"stick ({stick.x:F2}, {stick.y:F2}), nudge dir {nudge.normalized}");

            transformer.ModifyVelocity(nudge.normalized * nudgeSpeed, rollDurationSeconds);
            StartCoroutine(RollRoutine(rollSign, transformer));
        }

        IEnumerator RollRoutine(float rollSign, VesselTransformer transformer)
        {
            _rolling = true;
            _nextAllowedTime = Time.time + rollDurationSeconds + cooldownSeconds;

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

                // Bridging prisms: orient along the actual travel direction each frame while
                // the displacement is live.
                var travel = _status.Speed * _status.Course + transformer.VelocityShift;
                transformer.BlockRotationOverride = travel.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(travel.normalized, transform.up)
                    : null;

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
        }
    }
}
