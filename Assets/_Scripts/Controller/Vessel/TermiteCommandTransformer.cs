using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Termite queen's flight model: a COMMANDER hull. Design record:
    /// <c>R_VesselActions/TERMITE.md</c> §4.
    ///
    /// <para><b>You do not fly the queen, you tell her where to go.</b> The local pilot points at a
    /// place (<see cref="TermiteCommander"/> — click, tap or the pad's cursor) and the queen glides
    /// there under her own power, accelerating out, braking into an arrival and HOVERING when she
    /// gets there. That is the 2024 prototype's <c>CommandVesselTransformer</c> idea, rebuilt on the
    /// fleet's flight model instead of beside it: speed and heading are PUBLISHED through
    /// <c>VesselStatus.Speed</c>/<c>Course</c> exactly as any hull does, so the trail spawner, the
    /// speed tunnel, the wings and every Speed reader see an ordinary moving vessel.</para>
    ///
    /// <para><b>Without a command, she is an ordinary hull.</b> An AI pilot writes a stick and a
    /// throttle and knows nothing about points; autopilot (the menu lava lamp, a pilot swap) is the
    /// same. So whenever there is no live command — no local commander, autopilot on, or the pilot
    /// has not pointed anywhere yet — every call falls through to the base transformer and the
    /// queen flies on the stick like the rest of the fleet. That is what keeps her playable by an
    /// AI in every mode, with nothing mode-specific to wire.</para>
    ///
    /// <para><b>Modifier channels stay live.</b> A danger-prism slow (<c>throttleMultiplier</c>) and
    /// an external knock (<c>velocityShift</c>) reach a commanded queen exactly as they reach a
    /// flown one — a drifting-into-danger immunity by omission is a LOCKED-design violation, and
    /// freezing a quantity means listing every writer (the vessel skill's rule 12).</para>
    ///
    /// <para><b>TIME's parameter on this hull includes the queen's speed</b> — the captains have
    /// always said "fastest soaring speed in class" for Termite Time. It is an
    /// <see cref="ElementalFloat"/> on THIS MonoBehaviour and is read with
    /// <see cref="ElementalFloat.EvaluateLive"/> at use time.</para>
    /// </summary>
    public class TermiteCommandTransformer : VesselTransformer
    {
        [Header("Commander flight")]
        [Tooltip("The queen's cruise toward a command point, world u/s. TIME scales it (1x at rest " +
                 "-> 1.6x at Time 10): the Time captains have always read 'fastest soaring speed'.")]
        [SerializeField] ElementalFloat commandCruiseSpeed = ElementalFloat.Multiplier(70f, 112f, Element.Time, 35f);

        [Tooltip("How fast she gets up to speed, u/s per second.")]
        [SerializeField, Min(1f)] float commandAcceleration = 90f;

        [Tooltip("How hard she brakes into an arrival, u/s per second. The arrival speed is " +
                 "sqrt(2 x this x distance), so she lands on the point rather than overshooting it.")]
        [SerializeField, Min(1f)] float commandDeceleration = 70f;

        [Tooltip("Within this distance of the point she has arrived and hovers.")]
        [SerializeField, Min(0.1f)] float arrivalRadius = 4f;

        [Tooltip("How quickly she turns to face her direction of travel (per second, exponential).")]
        [SerializeField, Min(0.1f)] float commandTurnSharpness = 3.5f;

        [Tooltip("Farthest a command point may be from the queen, world units. A click far outside " +
                 "the arena would otherwise send her on a minute-long flight into nothing.")]
        [SerializeField, Min(10f)] float maxCommandDistance = 1600f;

        Vector3? _commandTarget;
        bool _commanderEnabled;
        float _commandSpeed;
        Vector3 _commandCourse = Vector3.forward;

        /// <summary>True while a local commander owns this queen's flight (set by
        /// <see cref="TermiteCommander"/> for the local human pilot only).</summary>
        public bool CommanderEnabled => _commanderEnabled;

        /// <summary>The current command point, if any.</summary>
        public Vector3? CommandTarget => _commandTarget;

        /// <summary>True when this frame's flight is the commander's rather than the stick's.</summary>
        public bool IsCommanding =>
            _commanderEnabled && _commandTarget.HasValue && VesselStatus != null && !VesselStatus.AutoPilotEnabled;

        /// <summary>Has she arrived at her command point (and is hovering)?</summary>
        public bool HasArrived => _commandTarget.HasValue &&
                                  (_commandTarget.Value - transform.position).sqrMagnitude <= arrivalRadius * arrivalRadius;

        /// <summary>Hand the flight to a local commander, or take it back (a swap to an AI, a
        /// pilot leaving). Taking it back clears the command and carries the current speed into
        /// the stick model so the hand-over does not stop her dead.</summary>
        public void SetCommanderEnabled(bool enabled)
        {
            if (_commanderEnabled == enabled) return;
            _commanderEnabled = enabled;
            if (!enabled)
            {
                _commandTarget = null;
                if (VesselStatus != null && _commandSpeed > 0f) SetInitialSpeedFromCommand(_commandSpeed);
                _commandSpeed = 0f;
            }
        }

        /// <summary>Send her to <paramref name="worldPoint"/> (clamped to the command reach).</summary>
        public void SetCommandTarget(Vector3 worldPoint)
        {
            Vector3 offset = worldPoint - transform.position;
            if (offset.sqrMagnitude > maxCommandDistance * maxCommandDistance)
                worldPoint = transform.position + offset.normalized * maxCommandDistance;

            // Carry the speed she already has into the first commanded frame, so a command issued
            // mid-flight bends her path rather than stopping her and starting again.
            if (!_commandTarget.HasValue && VesselStatus != null)
                _commandSpeed = Mathf.Max(_commandSpeed, VesselStatus.Speed);

            _commandTarget = worldPoint;
        }

        /// <summary>Forget the command point; she coasts to a hover where she is.</summary>
        public void ClearCommandTarget() => _commandTarget = transform.position;

        public override void ResetTransformer()
        {
            base.ResetTransformer();
            _commandTarget = null;
            _commandSpeed = 0f;
        }

        protected override void MoveShip()
        {
            if (!IsCommanding) { base.MoveShip(); return; }

            float dt = Time.deltaTime;
            Vector3 toTarget = _commandTarget.Value - transform.position;
            float distance = toTarget.magnitude;

            float cruise = Mathf.Max(0f, commandCruiseSpeed.EvaluateLive(VesselStatus));
            // Arrival: never faster than the speed from which she can still stop on the point.
            float arrival = Mathf.Sqrt(2f * commandDeceleration * Mathf.Max(0f, distance - arrivalRadius));
            float desired = distance <= arrivalRadius ? 0f : Mathf.Min(cruise, arrival);

            float rate = desired > _commandSpeed ? commandAcceleration : commandDeceleration;
            _commandSpeed = Mathf.MoveTowards(_commandSpeed, desired, rate * dt);

            if (distance > 1e-3f) _commandCourse = toTarget / distance;

            // Modifier channels: a slow scales her, a knock displaces her — both, always.
            float speed = _commandSpeed * Mathf.Max(0f, throttleMultiplier);
            transform.position += (_commandCourse * speed + velocityShift) * dt;

            VesselStatus.Speed = speed;
            VesselStatus.Course = _commandCourse;
        }

        protected override void RotateShip()
        {
            if (!IsCommanding) { base.RotateShip(); return; }

            // Face the way she is going while moving; hold her heading while hovering (a queen at
            // her mound does not spin to face nothing). Level to world up, so a commander's view
            // of her never rolls — the drag owns the camera's frame, and a rolling subject under a
            // steady camera reads as the subject being broken.
            if (_commandSpeed < 1f) return;

            if (!SafeLookRotation.TryGet(_commandCourse, Vector3.up, out var face, this, logError: false))
                return;
            float t = 1f - Mathf.Exp(-commandTurnSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, t);
        }

        void SetInitialSpeedFromCommand(float speedNow)
        {
            // The base's own speed seed, so the stick model picks up where the commander left off.
            // LOCAL on purpose: the transformer runs only on the machine that simulates this hull,
            // and IVessel.SetInitialSpeed sends a ClientRpc, which a client-owned queen may not.
            SetInitialSpeed(speedNow);
        }
    }
}
