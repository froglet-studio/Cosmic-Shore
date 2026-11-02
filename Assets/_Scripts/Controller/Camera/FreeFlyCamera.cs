using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The lava lamp's free-fly camera model (god mode, <see cref="MenuGodModeCamera"/>), shared so every free
    /// camera in the game flies the same way. The menu's god mode and the in-game AI Diagnose panel's Free fly
    /// (<c>ThirdEye</c>) both run this class with these defaults (Yash, 2026-11-01: "make sure Switchback has the camera
    /// settings that Lavalamp uses for camera fly mode").
    ///
    /// <para><b>Controls</b> (unscaled time): W A S D move along the view, E / Space up, Q / Ctrl down (world up), hold the
    /// right mouse button to look (cursor locked while held), Shift fast, Alt slow, mouse wheel scales the cruise speed
    /// (x1.2 a notch). Pad: left stick move, right stick look, RT up, LT down, RB fast. Velocity eases toward the input
    /// and glides out when you let go; the view eases toward its look target.</para>
    ///
    /// <para>Plain C#, no MonoBehaviour: the owner decides which camera transform it moves and when.</para>
    /// </summary>
    [Serializable]
    public sealed class FreeFlyCamera
    {
        [Tooltip("Cruise speed in world units per second. The mouse wheel scales it at runtime.")]
        [Min(0.1f)] public float moveSpeed = 60f;

        [Tooltip("Limits for the wheel-scaled cruise speed.")]
        public Vector2 moveSpeedRange = new(2f, 2000f);

        [Tooltip("Each wheel notch multiplies the cruise speed by this.")]
        [Min(1.01f)] public float wheelSpeedStep = 1.2f;

        [Tooltip("Speed multiplier while Shift (or RB) is held.")]
        [Min(1f)] public float fastMultiplier = 4f;

        [Tooltip("Speed multiplier while Alt is held.")]
        [Range(0.01f, 1f)] public float slowMultiplier = 0.25f;

        [Tooltip("How quickly velocity reaches the input speed (1/s). Higher = snappier start.")]
        [Min(0.1f)] public float acceleration = 6f;

        [Tooltip("How quickly velocity dies once input stops (1/s). Lower = longer glide.")]
        [Min(0.1f)] public float damping = 4f;

        [Tooltip("Degrees per mouse pixel while the right button is held.")]
        [Min(0.001f)] public float mouseDegreesPerPixel = 0.12f;

        [Tooltip("Degrees per second at full right-stick deflection.")]
        [Min(1f)] public float stickDegreesPerSecond = 120f;

        [Tooltip("How quickly the view follows the look target (1/s). Higher = tighter, lower = floatier. " +
                 "Also how fast any roll the starting camera had is levelled out.")]
        [Min(0.1f)] public float lookSmoothing = 14f;

        [Tooltip("Pitch limit, degrees, short of straight up/down.")]
        [Range(10f, 89.9f)] public float maxPitch = 89f;

        [Tooltip("Lock and hide the cursor while the right mouse button looks.")]
        public bool lockCursorWhileLooking = true;

        [NonSerialized] Vector3 _velocity;
        [NonSerialized] Quaternion _rot = Quaternion.identity;
        [NonSerialized] float _yaw, _pitch, _speed = -1f;
        [NonSerialized] bool _cursorCaptured, _savedVisible;
        [NonSerialized] CursorLockMode _savedLock;

        /// <summary>The current (wheel-scaled) cruise speed, u/s.</summary>
        public float Speed
        {
            get => _speed < 0f ? _speed = Mathf.Clamp(moveSpeed, moveSpeedRange.x, moveSpeedRange.y) : _speed;
            set => _speed = Mathf.Clamp(value, moveSpeedRange.x, moveSpeedRange.y);
        }

        /// <summary>Start (or restart) flying from this view: no velocity, look target = the view without its roll.</summary>
        public void Begin(Quaternion rotation)
        {
            _rot = rotation;
            Vector3 e = rotation.eulerAngles;
            _yaw = e.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -maxPitch, maxPitch);
            _velocity = Vector3.zero;
            _ = Speed;
        }

        /// <summary>Stop: no velocity, the cursor handed back.</summary>
        public void End()
        {
            _velocity = Vector3.zero;
            ReleaseCursor();
        }

        /// <summary>
        /// One frame of flight. <paramref name="keys"/>: keyboard and pad move (off while typing);
        /// <paramref name="look"/>: right-mouse look; <paramref name="wheel"/>: the wheel speed (the lava lamp turns it off
        /// over UI, the AI Diagnose panel turns both off over its panel).
        /// </summary>
        public void Tick(float dt, bool keys, bool look, bool wheel, ref Vector3 position, out Quaternion rotation)
        {
            rotation = _rot;
            if (dt <= 0f) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            // Look
            Vector2 delta = Vector2.zero;
            if (look && mouse != null && mouse.rightButton.isPressed)
            {
                if (lockCursorWhileLooking) CaptureCursor();
                delta += mouse.delta.ReadValue() * mouseDegreesPerPixel;
            }
            else ReleaseCursor();
            if (keys && pad != null) delta += pad.rightStick.ReadValue() * (stickDegreesPerSecond * dt);
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, -maxPitch, maxPitch);
            _rot = Quaternion.Slerp(_rot, Quaternion.Euler(_pitch, _yaw, 0f), 1f - Mathf.Exp(-lookSmoothing * dt));

            // Speed
            if (wheel && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    // Raw wheel is ±120 per notch on Windows, ±1 elsewhere; count notches either way.
                    float notches = Mathf.Abs(scroll) >= 30f ? scroll / 120f : Mathf.Sign(scroll);
                    Speed = Speed * Mathf.Pow(wheelSpeedStep, notches);
                }
            }

            // Move
            Vector3 input = Vector3.zero;
            float mult = 1f;
            if (keys && kb != null)
            {
                input.x += Axis(kb.dKey.isPressed, kb.aKey.isPressed);
                input.z += Axis(kb.wKey.isPressed, kb.sKey.isPressed);
                input.y += Axis(kb.eKey.isPressed || kb.spaceKey.isPressed, kb.qKey.isPressed || kb.ctrlKey.isPressed);
                if (kb.shiftKey.isPressed) mult *= fastMultiplier;
                if (kb.altKey.isPressed) mult *= slowMultiplier;
            }
            if (keys && pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                input.x += stick.x;
                input.z += stick.y;
                input.y += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (pad.rightShoulder.isPressed) mult *= fastMultiplier;
            }
            input = Vector3.ClampMagnitude(input, 1f);

            // Forward/strafe follow the view; up/down is world up, so climbing never drifts sideways.
            Vector3 wish = (_rot * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y) * (Speed * mult);
            float rate = input.sqrMagnitude > 1e-4f ? acceleration : damping;
            _velocity = Vector3.Lerp(_velocity, wish, 1f - Mathf.Exp(-rate * dt));
            if (input.sqrMagnitude <= 1e-4f && _velocity.sqrMagnitude < 1e-6f) _velocity = Vector3.zero;

            position += _velocity * dt;
            rotation = _rot;
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        void CaptureCursor()
        {
            if (_cursorCaptured) return;
            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorCaptured = true;
        }

        void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            _cursorCaptured = false;
        }
    }
}
