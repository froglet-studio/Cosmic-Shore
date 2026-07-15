using System.Collections;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// TIME level-5 'Barrel Roll' (ElementalAbilityMapSO upgrade). While boosting with the
    /// right stick at the circle's perimeter, the vessel rolls — clockwise on the right half
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
        [Tooltip("Right-stick radial magnitude treated as 'at the perimeter'. Uses the " +
                 "normalized (radially clamped) stick vector, never the eased one — the " +
                 "per-axis ease makes diagonal magnitudes direction-dependent.")]
        [SerializeField, Range(0.5f, 1f)] float perimeterThreshold = 0.95f;
        [Tooltip("Seconds after a roll completes before another can trigger.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 1.2f;
        [Tooltip("Left-stick magnitude below which the nudge defaults to the roll direction.")]
        [SerializeField, Range(0f, 1f)] float nudgeDeadzone = 0.2f;

        [Header("Roll")]
        [SerializeField, Min(0.1f)] float rollDurationSeconds = 0.6f;
        [Tooltip("Peak sideways displacement speed injected through ModifyVelocity (world " +
                 "units/second; the transformer clamps its channel at 100).")]
        [SerializeField, Min(0f)] float nudgeSpeed = 60f;
        [Tooltip("The transform that visually rolls. Defaults to the model's Animator " +
                 "transform, then the vessel root's first child.")]
        [SerializeField] Transform rollVisualTarget;

        // Interface-typed: AutoPilotEnabled and InputStatus are default interface members on
        // IVesselStatus (routed through AIPilot/Player) and are not visible on the concrete
        // VesselStatus type.
        IVesselStatus _status;
        bool _rolling;
        float _nextAllowedTime;
        Quaternion _visualRestRotation;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
        }

        void Update()
        {
            if (_status == null || _rolling || Time.time < _nextAllowedTime) return;
            if (_status.AutoPilotEnabled) return;
            if (_status.IsTranslationRestricted) return;
            if (!_status.IsBoosting) return;
            if (!_status.ElementalAbilityHandler.IsUpgradeActive(Element.Time)) return;

            var input = _status.InputStatus;
            if (input == null) return;

            // Trigger on WHICHEVER stick is at the perimeter. The gesture is "steering
            // deflection at maximum + boost" — on single-stick vessels (Sparrow) the
            // flight stick is the LEFT one and the right stick is free, so demanding the
            // right stick specifically made the upgrade untriggerable with the natural
            // flying grip. Either stick at the rim arms the roll.
            var right = input.RightNormalizedJoystickPosition;
            var left  = input.LeftNormalizedJoystickPosition;
            var trigger = right.magnitude >= left.magnitude ? right : left;
            if (trigger.magnitude < perimeterThreshold) return;

            // Right half of the circle → clockwise (positive angle about +forward is CW
            // from the pilot's seat in Unity's left-handed space); left half → CCW.
            float rollSign = trigger.x >= 0f ? 1f : -1f;

            var transformer = _status.VesselTransformer;
            if (!transformer) return;

            // The left stick picks the orthogonal nudge direction (stick up = nudge up);
            // if it is neutral, the trigger stick's deflection is used, then the roll
            // side. Projected onto the plane orthogonal to travel so the displacement
            // never adds forward/backward speed.
            var ship = _status.ShipTransform ? _status.ShipTransform : transform;
            var nudgeInput = left.magnitude >= nudgeDeadzone ? left : trigger;
            Vector3 nudge = ship.right * nudgeInput.x + ship.up * nudgeInput.y;
            nudge = Vector3.ProjectOnPlane(nudge, _status.Course);
            if (nudge.sqrMagnitude < 1e-4f)
                nudge = ship.right * rollSign;

            CSDebug.Log($"[BarrelRoll] Triggered: {(rollSign > 0f ? "CW" : "CCW")}, " +
                        $"trigger stick mag {trigger.magnitude:F2}, nudge dir {nudge.normalized}");

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
