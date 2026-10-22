using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The phone held as a steering wheel: a stick in [-1, 1] from how far the phone has tilted since
    /// <see cref="Engage"/>. Read from the gravity sensor (the OS's fused, smooth gravity), falling back
    /// to the raw accelerometer smoothed here. The angles and their signs are <see cref="TiltMath"/>.
    ///
    /// <para><b>Sensors start switched off.</b> The Input System leaves every sensor disabled until code
    /// enables it, which is why the old orientation flip never fired (MOBILE_VESSELS.md D12). This enables
    /// the one it reads on first use and never disables it: a hull that tilts once is likely to again.</para>
    /// </summary>
    public sealed class TouchTilt
    {
        Vector2 _reference;
        Vector2 _stick;
        Vector3 _smoothedGravity;
        bool _engaged;
        bool _hasGravity;

        public bool Engaged => _engaged;

        /// <summary>The last stick <see cref="Sample"/> produced (zero while disengaged).</summary>
        public Vector2 Stick => _engaged ? _stick : Vector2.zero;

        /// <summary>True when the device has a sensor this can read.</summary>
        public static bool Available => GravitySensor.current != null || Accelerometer.current != null;

        /// <summary>Takes the phone's current pose as neutral and starts producing a stick.</summary>
        public void Engage()
        {
            _hasGravity = TryReadGravity(out var gravity, out _);
            _smoothedGravity = gravity;
            _reference = _hasGravity ? TiltMath.Angles(gravity, LandscapeRight) : Vector2.zero;
            _stick = Vector2.zero;
            _engaged = true;
        }

        public void Disengage()
        {
            _engaged = false;
            _stick = Vector2.zero;
        }

        /// <summary>Advances the stick one frame and returns it. Zero while disengaged or with no sensor.</summary>
        public Vector2 Sample(float deltaTime, TouchControlProfileLibrarySO tuning)
        {
            if (!_engaged || !TryReadGravity(out var gravity, out bool fused)) return Vector2.zero;

            float full = tuning ? tuning.TiltFullScaleDegrees : 25f;
            float dead = tuning ? tuning.TiltDeadZoneDegrees : 2.5f;
            float rate = tuning ? tuning.TiltSmoothingPerSecond : 12f;

            // A first reading after Engage found no sensor: take this one as neutral instead.
            if (!_hasGravity)
            {
                _hasGravity = true;
                _smoothedGravity = gravity;
                _reference = TiltMath.Angles(gravity, LandscapeRight);
            }

            // No steering lag (Garrett, 2026-10-21): the OS's gravity sensor is already fused and
            // filtered, so it is read as it comes. Only the raw accelerometer fallback - which carries
            // every jolt of the hand - is smoothed.
            _smoothedGravity = fused ? gravity : Vector3.Lerp(_smoothedGravity, gravity, 1f - Mathf.Exp(-rate * deltaTime));
            _stick = TiltMath.Stick(TiltMath.Angles(_smoothedGravity, LandscapeRight), _reference, full, dead);
            return _stick;
        }

        internal static bool LandscapeRight => Screen.orientation == ScreenOrientation.LandscapeRight;

        /// <summary>The OS's fused gravity, else the raw accelerometer (<paramref name="fused"/> false), in the
        /// device frame. Enables the sensor it reads on first use.</summary>
        internal static bool TryReadGravity(out Vector3 gravity, out bool fused)
        {
            fused = true;
            var sensor = GravitySensor.current;
            if (sensor != null)
            {
                if (!sensor.enabled) InputSystem.EnableDevice(sensor);
                gravity = sensor.gravity.ReadValue();
                if (gravity.sqrMagnitude > 1e-4f) return true;
            }

            fused = false;

            var accelerometer = Accelerometer.current;
            if (accelerometer != null)
            {
                if (!accelerometer.enabled) InputSystem.EnableDevice(accelerometer);
                gravity = accelerometer.acceleration.ReadValue();
                if (gravity.sqrMagnitude > 1e-4f) return true;
            }

            gravity = Vector3.zero;
            return false;
        }
    }
}
