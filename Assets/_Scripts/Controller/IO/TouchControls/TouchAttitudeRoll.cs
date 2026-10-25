using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The phone as an augmented-reality window on the roll axis (the Dolphin's aim; Garrett, 2026-10-22:
    /// "1 to 1 roll like augmented reality not a control that sends the vessel into a roll"). Produces an
    /// ANGLE, not a stick: how far the phone has turned like a steering wheel since <see cref="Engage"/>,
    /// clockwise positive, held within the limit either side. <see cref="VesselTransformer"/> rolls the hull
    /// by exactly that angle (<see cref="IInputStatus.HoldsRollAngle"/>).
    ///
    /// <para><b>Read from the phone's pose.</b> The attitude sensor is the OS's fused orientation - gyro,
    /// accelerometer and compass (Android's rotation vector) - so the roll follows the wrist with no lag
    /// and holds with the phone at any pitch, flat included. A phone without one falls back to the gravity
    /// sensor's wheel angle (<see cref="TiltMath.Angles"/>), which is the same angle while the phone is
    /// held up toward the face.</para>
    ///
    /// <para><b>Sensors start switched off</b> (MOBILE_VESSELS.md D12): this enables the one it reads on
    /// first use and never disables it. A fresh sensor can report identity before its first real sample,
    /// so neutral is taken from the first non-identity reading, not from the call to Engage.</para>
    /// </summary>
    public sealed class TouchAttitudeRoll
    {
        enum Source { None = 0, Attitude = 1, Gravity = 2 }

        Source _source;
        Quaternion _attitudeReference;
        float _gravityReference;
        float _degrees;
        bool _engaged;

        public bool Engaged => _engaged;

        /// <summary>Takes the phone's pose at its next reading as neutral (zero roll).</summary>
        public void Engage()
        {
            _engaged = true;
            _source = Source.None;
            _degrees = 0f;
        }

        public void Disengage()
        {
            _engaged = false;
            _degrees = 0f;
        }

        /// <summary>Advances one frame and returns the held roll in degrees, clockwise positive, within
        /// <paramref name="maxDegrees"/> either side. Holds the last angle while no sensor reads.</summary>
        public float Sample(float maxDegrees)
        {
            if (!_engaged) return 0f;

            if (_source != Source.Gravity && TryReadAttitude(out var attitude))
            {
                if (_source == Source.None)
                {
                    _source = Source.Attitude;
                    _attitudeReference = attitude;
                }
                _degrees = TiltMath.RollBetween(_attitudeReference, attitude);
            }
            else if (_source != Source.Attitude && TouchTilt.TryReadGravity(out var gravity, out _))
            {
                float wheel = TiltMath.Angles(gravity, TouchTilt.LandscapeRight).x;
                if (_source == Source.None)
                {
                    _source = Source.Gravity;
                    _gravityReference = wheel;
                }
                _degrees = Mathf.DeltaAngle(_gravityReference, wheel);
            }

            _degrees = Mathf.Clamp(_degrees, -maxDegrees, maxDegrees);
            return _degrees;
        }

        /// <summary>Wakes the attitude sensor ahead of the first aim, so its first sample is in by then.</summary>
        public static void Warm()
        {
            var sensor = AttitudeSensor.current;
            if (sensor != null && !sensor.enabled) InputSystem.EnableDevice(sensor);
        }

        static bool TryReadAttitude(out Quaternion attitude)
        {
            attitude = Quaternion.identity;
            var sensor = AttitudeSensor.current;
            if (sensor == null) return false;
            if (!sensor.enabled) InputSystem.EnableDevice(sensor);
            attitude = sensor.attitude.ReadValue();

            // Not a pose yet: a zero quaternion (no sample) or exact identity (the default before one).
            float norm = attitude.x * attitude.x + attitude.y * attitude.y + attitude.z * attitude.z + attitude.w * attitude.w;
            if (norm < 0.5f) return false;
            return !(attitude.x == 0f && attitude.y == 0f && attitude.z == 0f);
        }
    }
}
