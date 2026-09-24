using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The theater's <b>free-flying camera</b> — Halo 3 Forge's monitor, in the shapes this
    /// project already has controls for.
    ///
    /// <para><b>Gamepad</b> (the intended way to fly it): left stick translates on the camera's own
    /// forward/right, right stick yaws and pitches, the triggers climb and dive, and the shoulders
    /// are the speed gears. <b>Keyboard and mouse</b> mirrors it: WASD translate, Q/E dive and
    /// climb, hold the RIGHT mouse button to look, arrow keys look without the mouse, Shift is
    /// boost and Ctrl is crawl.</para>
    ///
    /// <para><b>The horizon is locked and that is deliberate.</b> Forge's monitor cannot roll, and
    /// neither can this: yaw accumulates about WORLD up and pitch is clamped short of vertical, so
    /// there is no orientation a director can get into and not get out of. A camera that can roll
    /// is a camera somebody ends up flying upside down by accident while trying to line up a
    /// shot — and unlike a vessel, a camera has no horizon of its own to tell them.</para>
    ///
    /// <para><b>It reads the devices directly</b>, the way <c>ScreenshotGesture</c> and
    /// <c>OverviewGesture</c> do, rather than going through <c>IInputStrategy</c>. The strategies
    /// exist to turn sticks into a VESSEL's flight parameters (a dual-stick mix, an eased virtual
    /// stick, a signed throttle); none of that means anything to a camera, and routing through one
    /// would make the theater's feel a function of which hull the player happens to be flying.</para>
    ///
    /// <para>Speed is expressed in units of the framed scene's own radius per second, so one
    /// authored number flies comfortably in a 200-unit skirmish and in a 3,000-unit arena. The
    /// caller supplies that scale each frame.</para>
    /// </summary>
    public class TheaterFreeCamera
    {
        const float DeadZone = 0.15f;
        const float PitchLimit = 89f;

        float _yaw;
        float _pitch;
        bool _seeded;

        /// <summary>Speed gear the shoulders/modifiers last selected, for the overlay to show.</summary>
        public float Gear { get; private set; } = 1f;

        /// <summary>
        /// Adopt a pose without moving the camera — called when the free cam takes over from
        /// another shot, so it starts exactly where the director was already looking instead of
        /// snapping to whatever yaw it last held.
        /// </summary>
        public void Seed(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg,
                -PitchLimit, PitchLimit);
            _seeded = true;
        }

        /// <summary>Whether <see cref="Seed"/> has run since the last <see cref="Reset"/>.</summary>
        public bool IsSeeded => _seeded;

        public void Reset() => _seeded = false;

        /// <summary>
        /// Advance one frame. <paramref name="sceneRadius"/> is the radius of the action being
        /// framed; <paramref name="moveSpeed"/> and <paramref name="lookSpeed"/> come from the
        /// config. Returns the new pose through <paramref name="position"/> /
        /// <paramref name="rotation"/>.
        /// </summary>
        public void Tick(float unscaledDeltaTime, float sceneRadius, float moveSpeed, float lookSpeed,
            ref Vector3 position, out Quaternion rotation)
        {
            var pad = Gamepad.current;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            // ---- look -------------------------------------------------------------------------
            Vector2 look = Vector2.zero;
            if (pad != null) look += Deadzoned(pad.rightStick.ReadValue());

            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed) look.x -= 1f;
                if (keyboard.rightArrowKey.isPressed) look.x += 1f;
                if (keyboard.downArrowKey.isPressed) look.y -= 1f;
                if (keyboard.upArrowKey.isPressed) look.y += 1f;
            }

            _yaw += look.x * lookSpeed * unscaledDeltaTime;
            _pitch -= look.y * lookSpeed * unscaledDeltaTime;

            // The mouse is a DELTA, not an axis, so it is already per-frame: multiplying it by
            // deltaTime would make a fast machine turn less for the same hand movement.
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * 0.12f;
                _pitch -= delta.y * 0.12f;
            }

            _pitch = Mathf.Clamp(_pitch, -PitchLimit, PitchLimit);
            rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // ---- gears ------------------------------------------------------------------------
            bool boost = (pad != null && pad.rightShoulder.isPressed)
                         || (keyboard != null && keyboard.leftShiftKey.isPressed);
            bool crawl = (pad != null && pad.leftShoulder.isPressed)
                         || (keyboard != null && keyboard.leftCtrlKey.isPressed);
            Gear = boost ? 4f : crawl ? 0.25f : 1f;

            // ---- translate --------------------------------------------------------------------
            Vector2 move = Vector2.zero;
            if (pad != null) move += Deadzoned(pad.leftStick.ReadValue());

            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) move.x -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.wKey.isPressed) move.y += 1f;
            }

            float lift = 0f;
            if (pad != null) lift += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            if (keyboard != null)
            {
                if (keyboard.eKey.isPressed) lift += 1f;
                if (keyboard.qKey.isPressed) lift -= 1f;
            }

            Vector3 velocity = rotation * new Vector3(move.x, 0f, move.y) + Vector3.up * lift;
            if (velocity.sqrMagnitude > 1f) velocity.Normalize();

            // Speed scales with the SCENE, not with the world: a director flying a 200-unit
            // skirmish and a 3,000-unit arena wants the same number of seconds to cross each.
            position += velocity * (moveSpeed * Gear * Mathf.Max(1f, sceneRadius) * unscaledDeltaTime);
        }

        static Vector2 Deadzoned(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= DeadZone) return Vector2.zero;

            // Rescale from the deadzone edge rather than clipping, so the first unit of travel
            // past the edge is the smallest input rather than a jump to 15%.
            return raw / magnitude * Mathf.Min(1f, (magnitude - DeadZone) / (1f - DeadZone));
        }
    }
}
