using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The steerable half of every shot that WATCHES somebody — orbit, chase, and the pilot's own
    /// view. One rig, three bases.
    ///
    /// <para><b>A following camera you cannot steer is a camera you are stuck behind.</b> The first
    /// cut posed each of these from the subject alone, which meant a director watching a replay had
    /// exactly the vantage the shot's author chose and no way to look at the thing they were
    /// actually curious about. So every subject shot carries a yaw, a pitch, a dolly and a lift the
    /// player owns, applied ON TOP of the shot's own framing: the shot decides where the camera
    /// lives, the player decides where it looks from.</para>
    ///
    /// <para><b>The only difference between the three shots is the BASIS the offsets are measured
    /// in</b>, which is why they are one class rather than three. A CHASE and a PILOT view ride the
    /// subject's own frame, so they turn as it turns and a barrel roll rolls the shot. An ORBIT is
    /// measured in WORLD space, so the subject can tumble without taking the camera with it — which
    /// is the whole reason to want an orbit rather than a chase.</para>
    ///
    /// <para>Offsets persist while you stay on a shot and reset when you re-select it, so pressing
    /// the same shot key twice re-centres. There is no separate reset control to learn.</para>
    /// </summary>
    public class TheaterSubjectCamera
    {
        const float DeadZone = 0.15f;
        const float PitchLimit = 85f;
        const float MinDolly = 0.15f;
        const float MaxDolly = 12f;

        float _yaw;
        float _pitch;
        float _dolly = 1f;
        float _lift;

        /// <summary>Speed gear the shoulder/modifier keys are holding, for the overlay.</summary>
        public float Gear { get; private set; } = 1f;

        /// <summary>True while the player has steered away from the shot's own framing.</summary>
        public bool IsOffset => _yaw != 0f || _pitch != 0f || _dolly != 1f || _lift != 0f;

        public void Reset()
        {
            _yaw = 0f;
            _pitch = 0f;
            _dolly = 1f;
            _lift = 0f;
        }

        /// <summary>Read the sticks and advance the player's offsets. Runs whether or not the
        /// recording is playing — a paused replay you can still look around is the point.</summary>
        public void Tick(float unscaledDeltaTime, float lookSpeed, float dollySpeed)
        {
            var pad = Gamepad.current;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            bool boost = (pad != null && pad.rightShoulder.isPressed)
                         || (keyboard != null && keyboard.leftShiftKey.isPressed);
            bool crawl = (pad != null && pad.leftShoulder.isPressed)
                         || (keyboard != null && keyboard.leftCtrlKey.isPressed);
            Gear = boost ? 3f : crawl ? 0.3f : 1f;

            // ---- look: orbit the subject ------------------------------------------------------
            Vector2 look = Vector2.zero;
            if (pad != null) look += Deadzoned(pad.rightStick.ReadValue());
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) look.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) look.x += 1f;
                if (keyboard.downArrowKey.isPressed) look.y -= 1f;
                if (keyboard.upArrowKey.isPressed) look.y += 1f;
            }

            _yaw += look.x * lookSpeed * Gear * unscaledDeltaTime;
            _pitch -= look.y * lookSpeed * Gear * unscaledDeltaTime;

            // The mouse is a DELTA, so it is already per-frame: multiplying by deltaTime would make
            // a fast machine turn less for the same hand movement.
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.12f;
                _pitch -= delta.y * 0.12f;
            }

            _pitch = Mathf.Clamp(_pitch, -PitchLimit, PitchLimit);

            // ---- dolly and lift ---------------------------------------------------------------
            float dolly = 0f;
            if (pad != null) dolly -= Deadzoned(pad.leftStick.ReadValue()).y;
            if (keyboard != null)
            {
                if (keyboard.sKey.isPressed) dolly += 1f;
                if (keyboard.wKey.isPressed) dolly -= 1f;
            }

            // Multiplicative, so one press moves the same FRACTION of the current distance whether
            // the camera is on the hull or a kilometre out. An additive dolly is unusable at both
            // ends of a fleet whose sizes span two orders of magnitude.
            if (dolly != 0f)
                _dolly = Mathf.Clamp(
                    _dolly * Mathf.Exp(dolly * dollySpeed * Gear * unscaledDeltaTime),
                    MinDolly, MaxDolly);

            float lift = 0f;
            if (pad != null) lift += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            if (keyboard != null)
            {
                if (keyboard.eKey.isPressed) lift += 1f;
                if (keyboard.qKey.isPressed) lift -= 1f;
            }
            _lift += lift * Gear * unscaledDeltaTime;
        }

        /// <summary>
        /// Pose the camera against a subject.
        /// </summary>
        /// <param name="subjectPosition">Where the watched ship is.</param>
        /// <param name="subjectRotation">How it is oriented.</param>
        /// <param name="baseOffset">The shot's own framing, in the basis frame: direction from the
        /// subject to the camera, and its length the resting distance.</param>
        /// <param name="worldBasis">True for an orbit (offsets measured in world space, so the
        /// subject can tumble without taking the shot with it); false for a chase or pilot view,
        /// which ride the subject's frame.</param>
        /// <param name="autoYaw">Extra yaw the shot itself is applying, e.g. an orbit's drift.</param>
        public void Pose(Vector3 subjectPosition, Quaternion subjectRotation, Vector3 baseOffset,
            bool worldBasis, float autoYaw, out Vector3 position, out Quaternion rotation)
        {
            float distance = baseOffset.magnitude * _dolly;
            if (distance < 0.001f) distance = 1f;

            Vector3 direction = baseOffset.sqrMagnitude > 1e-6f
                ? baseOffset.normalized
                : new Vector3(0f, 0.25f, -1f).normalized;

            Quaternion basis = worldBasis ? Quaternion.identity : subjectRotation;
            Quaternion steer = Quaternion.Euler(_pitch, _yaw + autoYaw, 0f);

            Vector3 up = worldBasis ? Vector3.up : basis * Vector3.up;
            position = subjectPosition + basis * (steer * direction) * distance + up * (_lift * distance);

            Vector3 toSubject = subjectPosition - position;
            rotation = toSubject.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(toSubject.normalized, up)
                : subjectRotation;
        }

        static Vector2 Deadzoned(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= DeadZone) return Vector2.zero;

            // Rescale from the deadzone edge rather than clipping, so the first unit of travel past
            // the edge is the smallest input rather than a jump to 15%.
            return raw / magnitude * Mathf.Min(1f, (magnitude - DeadZone) / (1f - DeadZone));
        }
    }
}
